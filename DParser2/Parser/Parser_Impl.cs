using System;
using System.Collections.Generic;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;

namespace D_Parser.Parser
{
	/// <summary>
	/// Parser for D Code
	/// </summary>
	public partial class DParser
	{
		#region Modules
		// http://www.digitalmars.com/d/2.0/module.html

		/// <summary>
		/// Module entry point
		/// </summary>
		public DModule Root()
		{
			Step();

			var module = new DModule();
			LastParsedObject = module;
			module.Location = la.Location;
			doc = module;

			// Only one module declaration possible!
			if (laKind == (Module))
			{
				module.Description = GetComments();
				module.OptionalModuleStatement= ModuleDeclaration();
				module.Add(module.OptionalModuleStatement);
				module.Description += CheckForPostSemicolonComment();

				if (module.OptionalModuleStatement.ModuleName!=null)
					module.ModuleName = module.OptionalModuleStatement.ModuleName.ToString();
				module.OptionalModuleStatement.ParentNode = doc;
			}

			// Now only declarations or other statements are allowed!
			while (!IsEOF)
			{
				DeclDef(module);
			}
			
			module.EndLocation = la.Location;
			return module;
		}

		#region Comments
		string PreviousComment = "";

		string GetComments()
		{
			string ret = "";

			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
					ret += c.CommentText + Environment.NewLine;
			}

			TrackerVariables.Comments.AddRange(Lexer.Comments);
			Lexer.Comments.Clear();

			ret = ret.Trim();

			if (String.IsNullOrEmpty(ret)) 
				return ""; 

			// Overwrite only if comment is not 'ditto'
			if (ret.ToLowerInvariant() != "ditto")
				PreviousComment = ret;

			return PreviousComment;
		}

		/// <summary>
		/// Returns the pre- and post-declaration comment
		/// </summary>
		/// <returns></returns>
		string CheckForPostSemicolonComment()
		{
			if (t == null)
				return "";

			int ExpectedLine = t.Line;

			string ret = "";

			int i=0;
			foreach (var c in Lexer.Comments)
			{
				if (c.CommentType.HasFlag(Comment.Type.Documentation))
				{
					// Ignore ddoc comments made e.g. in int a /** ignored comment */, b,c; 
					// , whereas this method is called as t is the final semicolon
					if (c.EndPosition <= t.Location)
					{
						i++;
						TrackerVariables.Comments.Add(c);
						continue;
					}
					else if (c.StartPosition.Line > ExpectedLine)
						break;

					ret += c.CommentText + Environment.NewLine;
					i++;

					TrackerVariables.Comments.Add(c);
				}
			}
			Lexer.Comments.RemoveRange(0, i);

			if (string.IsNullOrEmpty(ret))
				return "";

			ret = ret.Trim();
			
			// Add post-declaration string if comment text is 'ditto'
			if (ret.ToLowerInvariant() == "ditto")
				return PreviousComment;

			// Append post-semicolon comment string to previously read comments
			if (!string.IsNullOrWhiteSpace(PreviousComment)) // If no previous comment given, do not insert a new-line
				return PreviousComment = ret;

			PreviousComment += Environment.NewLine + ret;
			return Environment.NewLine + ret;
		}

		#endregion

		void DeclDef(DBlockNode module)
		{
			//AttributeSpecifier
			while (IsAttributeSpecifier())
			{
				AttributeSpecifier(module);

				if (t.Kind == Colon || laKind == CloseCurlyBrace || IsEOF)
					return;
			}

			if (laKind == Semicolon)
			{
				LastParsedObject = null;
				Step();
				return;
			}

			//ImportDeclaration
			if (laKind == Import)
				module.Add(ImportDeclaration());

			//Constructor
			else if (laKind == (This))
				module.Add(Constructor(module, module is DClassLike ? ((DClassLike)module).ClassType == DTokens.Struct : false));

			//Destructor
			else if (laKind == (Tilde) && Lexer.CurrentPeekToken.Kind == (This))
				module.Add(Destructor());

			//Invariant
			else if (laKind == (Invariant))
				module.Add(_Invariant());

			//UnitTest
			else if (laKind == (Unittest))
			{
				Step();
				var dbs = new DMethod(DMethod.MethodType.Unittest);
				LastParsedObject = dbs;
				dbs.Location = t.Location;
				FunctionBody(dbs);
				dbs.EndLocation = t.EndLocation;
				module.Add(dbs);
			}

			/*
			 * VersionSpecification: 
			 *		version = Identifier ; 
			 *		version = IntegerLiteral ;
			 * 
			 * DebugSpecification: 
			 *		debug = Identifier ; 
			 *		debug = IntegerLiteral ;
			 */
			else if ((laKind == Version || laKind == Debug) && Peek(1).Kind == Assign)
			{
				Step();

				var ass = new VersionDebugSpecification
				{
					Token = t.Kind,
					Location = t.Location
				};
				LastParsedObject = ass;

				Step();

				if (laKind == Literal)
				{
					Step();
					ass.SpecifiedValue = new IdentifierExpression(t.LiteralValue, t.LiteralFormat)
						{
							Location = t.Location,
							EndLocation = t.EndLocation
						};
				}
				else if (Expect(Identifier))
					ass.SpecifiedValue = new IdentifierExpression(t.LiteralValue, t.LiteralFormat)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};

				Expect(Semicolon);

				ass.EndLocation = t.EndLocation;

				module.Add(ass);
			}

			else if (laKind == Version || laKind == Debug || laKind == If)
				DeclarationCondition(module);

			//StaticAssert
			else if (laKind == (Assert))
			{
				Step();

				if (DAttribute.ContainsAttribute(DeclarationAttributes, Static))
				{
					//HACK: Assume that there's only our 'static' attribute applied to the 'if'-statement
					DeclarationAttributes.Clear();
				}
				else
					SynErr(Static, "Static assert statements must be explicitly marked as static");

				if (Expect(OpenParenthesis))
				{
					AssignExpression();
					if (laKind == (Comma))
					{
						Step();
						AssignExpression();
					}
					Expect(CloseParenthesis);
				}
				Expect(Semicolon);
			}

			//TemplateMixinDeclaration
			else if (laKind == Mixin)
			{
				if (Peek(1).Kind == Template)
					module.Add(TemplateDeclaration(module));

				//TemplateMixin
				else if (Lexer.CurrentPeekToken.Kind == Identifier)
					module.Add(TemplateMixin());

				//MixinDeclaration
				else if (Lexer.CurrentPeekToken.Kind == OpenParenthesis)
					module.Add(MixinDeclaration());
				else
				{
					Step();
					SynErr(Identifier);
				}
			}

			// {
			else if (laKind == (OpenCurlyBrace))
				AttributeBlock(module);

			// Class Allocators
			// Note: Although occuring in global scope, parse it anyway but declare it as semantic nonsense;)
			else if (laKind == (New))
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Allocator) { Location = t.Location };
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				dm.EndLocation = t.EndLocation;
				module.Add(dm);
			}

			// Class Deallocators
			else if (laKind == Delete)
			{
				Step();

				var dm = new DMethod(DMethod.MethodType.Deallocator) { Location = t.Location };
				dm.Name = "delete";
				ApplyAttributes(dm);

				dm.Parameters = Parameters(dm);
				FunctionBody(dm);
				dm.EndLocation = t.EndLocation;
				module.Add(dm);
			}

			// else:
			else
			{
				var decls = Declaration(module);
				if(module != null && decls!=null)
					module.AddRange(decls);
			}
		}

		AbstractMetaDeclaration AttributeBlock(DBlockNode module)
		{
			int popCount = DeclarationAttributes.Count;

			/*
			 * If there are attributes given, put their references into the meta block.
			 * Also, pop them from the declarationAttributes stack on to the block attributes so they will be assigned to all child items later on.
			 */

			AbstractMetaDeclaration metaDeclBlock;

			if (popCount != 0)
				metaDeclBlock = new AttributeMetaDeclarationBlock(DeclarationAttributes.ToArray()) { BlockStartLocation = la.Location };
			else
				metaDeclBlock = new MetaDeclarationBlock { BlockStartLocation = la.Location };

			while (DeclarationAttributes.Count > 0)
				BlockAttributes.Push(DeclarationAttributes.Pop());

			ClassBody(module, true, false);

			// Pop the previously pushed attributes back off the stack
			for (int i = popCount; i > 0; i--)
				BlockAttributes.Pop();

			// Store the meta block
			metaDeclBlock.EndLocation = t.EndLocation;
			if(module!=null)
				module.Add(metaDeclBlock);
			return metaDeclBlock;
		}

		void DeclarationCondition(DBlockNode module)
		{
			Step();

			var c = new DeclarationCondition(t.Kind) { Location = t.Location };
			LastParsedObject = c;

			/* 
			 * http://www.d-programming-language.org/version.html#VersionSpecification
			 * VersionCondition: 
			 *		version ( IntegerLiteral ) 
			 *		version ( Identifier ) 
			 *		version ( unittest )
			 */
			if (c.IsVersionCondition && Expect(OpenParenthesis))
			{
				if (laKind == Unittest)
				{
					Step();
					c.Condition = new TokenExpression(Unittest) { Location = t.Location, EndLocation = t.EndLocation };
				}
				else if (laKind == Literal)
				{
					Step();
					c.Condition = new IdentifierExpression(t.LiteralValue, t.LiteralFormat)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				}
				else if (Expect(Identifier))
					c.Condition = new IdentifierExpression(t.Value, t.LiteralFormat)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}

			/*
			 * DebugCondition:
			 *		debug 
			 *		debug ( IntegerLiteral )
			 *		debug ( Identifier )
			 */
			else if (c.IsDebugCondition)
			{
				if (laKind == OpenParenthesis)
				{
					Step();

					if (laKind == Literal)
					{
						Step();
						c.Condition = new IdentifierExpression(t.LiteralValue, t.LiteralFormat)
						{
							Location = t.Location,
							EndLocation = t.EndLocation
						};
					}
					else if (Expect(Identifier))
						c.Condition = new IdentifierExpression(t.Value, t.LiteralFormat)
						{
							Location = t.Location,
							EndLocation = t.EndLocation
						};

					Expect(CloseParenthesis);
				}
			}

			/*
			 * StaticIfCondition: 
			 *		static if ( AssignExpression )
			 */
			else if (c.IsStaticIfCondition && Expect(OpenParenthesis))
			{
				if (DAttribute.ContainsAttribute(DeclarationAttributes, Static))
					DeclarationAttributes.Clear();
				else
					SynErr(Static, "Conditional declaration checks must be static");

				c.Condition = AssignExpression();

				Expect(CloseParenthesis);
			}

			c.EndLocation = t.EndLocation;

			bool allowElse = laKind != Colon;

			var metaBlock = AttributeTrail(module, c, true) as AttributeMetaDeclaration;

			if (allowElse && metaBlock == null)
			{
				SynErr(c.Token, "Wrong meta block type. (see DeclarationCondition();)");
				return;
			}
			else if (allowElse && laKind == Else)
			{
				Step();

				c = c.Clone() as DeclarationCondition;
				c.Negate();

				if (laKind == OpenCurlyBrace)
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclarationBlock { Location = t.Location, BlockStartLocation = la.Location };
					BlockAttributes.Push(c);

					ClassBody(module, true, false);

					BlockAttributes.Pop();
				}
				else
				{
					metaBlock.OptionalElseBlock = new ElseMetaDeclaration { Location = t.Location };
					DeclarationAttributes.Push(c);

					DeclDef(module);
				}

				metaBlock.OptionalElseBlock.EndLocation = t.EndLocation;
			}
		}

		ModuleStatement ModuleDeclaration()
		{
			Expect(Module);
			var ret = new ModuleStatement { Location=t.Location };
			LastParsedObject = ret;
			ret.ModuleName = ModuleFullyQualifiedName();
			if (Expect(Semicolon))
				LastParsedObject = null;
			ret.EndLocation = t.EndLocation;
			return ret;
		}

		ITypeDeclaration ModuleFullyQualifiedName()
		{
			Expect(Identifier);

			var td = new IdentifierDeclaration(t.Value) { Location=t.Location,EndLocation=t.EndLocation };

			while (laKind == Dot)
			{
				Step();
				Expect(Identifier);

				var td2 = new IdentifierDeclaration(t.Value) { Location=t.Location, EndLocation=t.EndLocation };

				td2.InnerDeclaration = td;
				td = td2;
			}

			return td;
		}

		ImportStatement ImportDeclaration()
		{
			Expect(Import);

			var importStatement = new ImportStatement { Location=t.Location };

			ApplyAttributes(importStatement);
			
			var imp = _Import();

			while (laKind == Comma)
			{
				importStatement.Imports.Add(imp);

				Step();

				imp = _Import();
			}

			if (laKind == Colon)
			{
				Step();
				importStatement.ImportBinding = ImportBindings(imp);
			}
			else
				importStatement.Imports.Add(imp); // Don't forget to add the last import

			if (Expect(Semicolon))
				LastParsedObject = null;

			CheckForPostSemicolonComment();

			importStatement.EndLocation = t.EndLocation;

			// Prepare for resolving external items
			importStatement.CreatePseudoAliases();

			return importStatement;
		}

		ImportStatement.Import _Import()
		{
			var import = new ImportStatement.Import();
			LastParsedObject = import;

			// ModuleAliasIdentifier
			if (Lexer.CurrentPeekToken.Kind == Assign)
			{
				if(Expect(Identifier))
					import.ModuleAlias = t.Value;
				Step();
			}

			import.ModuleIdentifier = ModuleFullyQualifiedName();

			if (!IsEOF)
				LastParsedObject = null;

			return import;
		}

		ImportStatement.ImportBindings ImportBindings(ImportStatement.Import imp)
		{
			var importBindings = new ImportStatement.ImportBindings { Module=imp };
			LastParsedObject = importBindings;

			bool init = true;
			while (laKind == Comma || init)
			{
				if (init)
					init = false;
				else
					Step();

				if (Expect(Identifier))
				{
					if (laKind == Assign)
					{
						var symbolAlias = t.Value;
						Step();
						if (Expect(Identifier))
							importBindings.SelectedSymbols.Add(new KeyValuePair<string, string>(symbolAlias, t.Value));
					}
					else
						importBindings.SelectedSymbols.Add(new KeyValuePair<string, string>(t.Value, null));
				}
			}

			if (!IsEOF)
				LastParsedObject = null;

			return importBindings;
		}


		IStatement MixinDeclaration()
		{
            var mx = AssignExpression();
			Expect(Semicolon);

            if (mx == null)
                return null;

            return new ExpressionStatement { Location=mx.Location, Expression=mx, EndLocation=t.EndLocation };
		}
		#endregion

		#region Declarations
		// http://www.digitalmars.com/d/2.0/declaration.html

		bool CheckForStorageClasses(DBlockNode scope)
		{
			bool ret = false;
			while (IsStorageClass || laKind==PropertyAttribute)
			{
				if (IsAttributeSpecifier()) // extern, align
					AttributeSpecifier(scope);
				else
				{
					Step();
					// Always allow more than only one property attribute
					if (t.Kind == PropertyAttribute || !DAttribute.ContainsAttribute(DeclarationAttributes.ToArray(), t.Kind))
						PushAttribute(new DAttribute(t.Kind, t.Value) { Location = t.Location, EndLocation = t.EndLocation }, false);
				}
				ret = true;
			}
			return ret;
		}

		public INode[] Declaration(IBlockNode Scope)
		{
			// Skip ref token
			if (laKind == (Ref))
			{
				PushAttribute(new DAttribute(Ref), false);
				Step();
			}

			// Enum possible storage class attributes
			bool HasStorageClassModifiers = CheckForStorageClasses(Scope as DBlockNode);

			if (laKind == (Alias) || laKind == Typedef)
			{
				Step();
				// _t is just a synthetic node which holds possible following attributes
				var _t = new DVariable();
				ApplyAttributes(_t);

				// AliasThis
				if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == This)
				{
                    var dv = new DVariable { 
                        Description = GetComments(),
                        Location=t.Location, 
                        IsAlias=true, 
                        Name="this",
						Parent = Scope
                    };
					LastParsedObject = dv;

                    Step();
                    dv.Type = new IdentifierDeclaration(t.Value)
                    {
                        Location=t.Location, 
                        EndLocation=t.EndLocation 
                    };

                    Step();
                    dv.NameLocation = t.Location;
					dv.EndLocation = t.EndLocation;
					
					Expect(Semicolon);
					dv.Description += CheckForPostSemicolonComment();

					return new[]{dv};
				}

				var decls=Decl(HasStorageClassModifiers,Scope);

				if(decls!=null && decls.Length>0)
					foreach (var n in decls)
					{
						if (n is DNode)
							((DNode)n).Attributes.AddRange(_t.Attributes);

						if (n is DVariable)
							((DVariable)n).IsAlias = true;
					}

				return decls;
			}
			else if (laKind == (Struct) || laKind == (Union))
				return new[]{ AggregateDeclaration(Scope)};
			else if (laKind == Enum)
			{
				// As another meta-programming feature, it is possible to create static functions 
				// that return enums, i.e. a constant value or something
				if (Lexer.CurrentPeekToken.Kind == Identifier && Peek().Kind == OpenParenthesis)
				{
					Peek(1);
					return Decl(HasStorageClassModifiers, Scope);
				}

				return EnumDeclaration(Scope);
			}
			else if (laKind == (Class))
				return new[]{ ClassDeclaration(Scope)};
			else if (laKind == (Template) || (laKind==Mixin && Peek(1).Kind==Template))
				return new[]{ TemplateDeclaration(Scope)};
			else if (laKind == (Interface))
				return new[]{ InterfaceDeclaration(Scope)};
			else if (IsBasicType() || laKind==Ref)
				return Decl(HasStorageClassModifiers,Scope);
			else
			{
				SynErr(laKind,"Declaration expected, not "+GetTokenString(laKind));
				Step();
			}
			return null;
		}

		INode[] Decl(bool HasStorageClassModifiers,IBlockNode Scope)
		{
			var startLocation = la.Location;
			var initialComment = GetComments();
			ITypeDeclaration ttd = null;

			CheckForStorageClasses(Scope as DBlockNode);
			// Skip ref token
			if (laKind == (Ref))
			{
				if (!DAttribute.ContainsAttribute(DeclarationAttributes, Ref))
					PushAttribute(new DAttribute(Ref), false);
				Step();
			}

			// Autodeclaration
			var StorageClass = DTokens.ContainsStorageClass(DeclarationAttributes.ToArray());

			//TODO: Specify how enum-returning functions shall be handled
			if (StorageClass.Token == DAttribute.Empty.Token && laKind == Enum)
			{
				Step();
				PushAttribute(StorageClass = new DAttribute(Enum) { Location = t.Location, EndLocation = t.EndLocation },false);
			}
			
			// If there's no explicit type declaration, leave our node's type empty!
			if ((StorageClass.Token != DAttribute.Empty.Token && 
				laKind == (Identifier) && DeclarationAttributes.Count > 0)) // public auto var=0; // const foo(...) {} 
			{
				if (Lexer.CurrentPeekToken.Kind == Assign || Lexer.CurrentPeekToken.Kind ==OpenParenthesis) 
				{ }
				else if (Lexer.CurrentPeekToken.Kind == Semicolon)
				{
					SemErr(StorageClass.Token, "Initializer expected for auto type, semicolon found!");
				}
				else
					ttd = BasicType();
			}
			else
				ttd = BasicType();

			/*
			 * T! -- tix.Arguments == null
			 * T!(int, -- last argument == null
			 * T!(int, bool, -- ditto
			 * T!(int) -- now every argument is complete
			 */
			var tix=ttd as TemplateInstanceExpression;
			if (IsEOF && tix!=null && (tix.Arguments == null ||
				tix.Arguments[tix.Arguments.Length-1] is TokenExpression &&
				((TokenExpression)tix.Arguments[tix.Arguments.Length-1]).Token == DTokens.INVALID))
			{
				LastParsedObject = ttd;
				return null;
			}

			// Declarators
			var firstNode = Declarator(ttd,false);
			firstNode.Description = initialComment;
			firstNode.Location = startLocation;

			ApplyAttributes(firstNode as DNode);

			// Check for declaration constraints
			if (laKind == (If))
				Constraint();

			// BasicType Declarators ;
			if (laKind==Assign || laKind==Comma || laKind==Semicolon)
			{
				// DeclaratorInitializer
				if (laKind == (Assign))
				{
					TrackerVariables.InitializedNode = firstNode;
					var dv = firstNode as DVariable;
					if(dv!=null)
						dv.Initializer = Initializer(Scope);
				}
				firstNode.EndLocation = t.EndLocation;
				var ret = new List<INode>();
				ret.Add(firstNode);

				// DeclaratorIdentifierList
				while (laKind == (Comma))
				{
					Step();
					Expect(Identifier);

					var otherNode = new DVariable();
					LastParsedObject = otherNode;

					/// Note: In DDoc, all declarations that are made at once (e.g. int a,b,c;) get the same pre-declaration-description!
					otherNode.Description = initialComment;

					otherNode.AssignFrom(firstNode);
					otherNode.Location = t.Location;
					otherNode.Name = t.Value;
					otherNode.NameLocation = t.Location;

					if (laKind == (Assign))
					{
						TrackerVariables.InitializedNode = otherNode;
						otherNode.Initializer = Initializer(Scope);
					}

					otherNode.EndLocation = t.EndLocation;
					ret.Add(otherNode);
				}

				if (Expect(Semicolon))
					LastParsedObject = null;

				// Note: In DDoc, only the really last declaration will get the post semicolon comment appended
				if (ret.Count > 0)
					ret[ret.Count - 1].Description += CheckForPostSemicolonComment();

				return ret.ToArray();
			}

			// BasicType Declarator FunctionBody
			else if (firstNode is DMethod && IsFunctionBody)
			{
				firstNode.Description += CheckForPostSemicolonComment();

				FunctionBody((DMethod)firstNode);

				firstNode.Description += CheckForPostSemicolonComment();

				return new[]{ firstNode};
			}
			else
				SynErr(OpenCurlyBrace, "; or function body expected after declaration stub.");

			return null;
		}

		bool IsBasicType()
		{
			return 
				BasicTypes[laKind] || 
				laKind == (Typeof) || 
				MemberFunctionAttribute[laKind] || 
				(laKind == (Dot) && Lexer.CurrentPeekToken.Kind == (Identifier)) || 
				//BasicTypes[Lexer.CurrentPeekToken.Kind] || 
				laKind == (Identifier);
		}

		/// <summary>
		/// Used if the parser is unsure if there's a type or an expression - then, instead of throwing exceptions, the Type()-Methods will simply return null;
		/// </summary>
		public bool AllowWeakTypeParsing = false;

		ITypeDeclaration BasicType()
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
				Step();

			ITypeDeclaration td = null;
			if (BasicTypes[laKind])
			{
				Step();
				return new DTokenDeclaration(t.Kind) { Location=t.Location, EndLocation=t.EndLocation };
			}

			if (MemberFunctionAttribute[laKind])
			{
				Step();
				var md = new MemberFunctionAttributeDecl(t.Kind) { Location=t.Location };
				bool p = false;

				if (laKind == OpenParenthesis)
				{
					Step();
					p = true;
				}

				// e.g. cast(const)
				if (laKind != CloseParenthesis)
					md.InnerType = Type();

				if (p)
					Expect(CloseParenthesis);
				md.EndLocation = t.EndLocation;
				return md;
			}

			//TODO
			if (laKind == Ref)
				Step();

			if (laKind == (Typeof))
			{
				td = TypeOf();
				if (laKind != Dot)
					return td;
			}

			else if (laKind == __vector)
			{
				td = Vector();
				if (laKind != Dot)
					return td;
			}

			if (AllowWeakTypeParsing && laKind != Identifier)
				return null;

			if (td == null)
				td = IdentifierList();
			else
				td.InnerMost = IdentifierList();

			// A type is never a declaration identifier
			if (td == null)
				ExpectingIdentifier = false;
			else if(isModuleScoped)
			{
				var innerMost = td.InnerMost;
				if (innerMost is IdentifierDeclaration)
					((IdentifierDeclaration)innerMost).ModuleScoped = true;
				else if (innerMost is TemplateInstanceExpression)
					((TemplateInstanceExpression)innerMost).TemplateIdentifier.ModuleScoped = true;
			}

			return td;
		}

		bool IsBasicType2()
		{
			return laKind == (Times) || laKind == (OpenSquareBracket) || laKind == (Delegate) || laKind == (Function);
		}

		ITypeDeclaration BasicType2()
		{
			// *
			if (laKind == (Times))
			{
				Step();
				return new PointerDecl() { Location=t.Location, EndLocation=t.EndLocation };
			}

			// [ ... ]
			else if (laKind == (OpenSquareBracket))
			{
				var startLoc = la.Location;
				Step();
				// [ ]
				if (laKind == (CloseSquareBracket)) 
				{ 
					Step();
					return new ArrayDecl() { Location=startLoc, EndLocation=t.EndLocation }; 
				}

				ITypeDeclaration cd = null;

				// [ Type ]
				var la_backup = la;
				bool weaktype = AllowWeakTypeParsing;
				AllowWeakTypeParsing = true;

				var keyType = Type();

				AllowWeakTypeParsing = weaktype;

				if (keyType != null && laKind == CloseSquareBracket && !(keyType is IdentifierDeclaration)) 
					//HACK: Both new int[size_t] as well as new int[someConstNumber] are legal. So better treat them as expressions.
					cd = new ArrayDecl() { KeyType = keyType, ClampsEmpty = false, Location = startLoc };
				else
				{
					la = la_backup;
					Peek(1);

					var fromExpression = AssignExpression();

					// [ AssignExpression .. AssignExpression ]
					if (laKind == DoubleDot)
					{
						Step();
						cd = new ArrayDecl() {
							Location=startLoc,
							ClampsEmpty=false,
							KeyType=null,
							KeyExpression= new PostfixExpression_Slice() { 
								FromExpression=fromExpression,
								ToExpression=AssignExpression()}};
					}
					else
						cd = new ArrayDecl() { KeyType=null, KeyExpression=fromExpression,ClampsEmpty=false,Location=startLoc };
				}

				if (AllowWeakTypeParsing && laKind != CloseSquareBracket)
					return null;

				Expect(CloseSquareBracket);
				if(cd!=null)
					cd.EndLocation = t.EndLocation;
				return cd;
			}

			// delegate | function
			else if (laKind == (Delegate) || laKind == (Function))
			{
				Step();
				var dd = new DelegateDeclaration() { Location=t.Location};
				dd.IsFunction = t.Kind == Function;

				var lpo = LastParsedObject;

				if (AllowWeakTypeParsing && laKind != OpenParenthesis)
					return null;

				dd.Parameters = Parameters(null);

				if (!IsEOF)
					LastParsedObject = lpo;

				var attributes = new List<DAttribute>();
				while (FunctionAttribute[laKind])
				{
					Step();
					attributes.Add(new DAttribute(t.Kind, t.Value) { Location = t.Location, EndLocation = t.EndLocation });
				}
				dd.Modifiers= attributes.Count>0? attributes.ToArray() : null;

				dd.EndLocation = t.EndLocation;
				return dd;
			}
			else
				SynErr(Identifier);
			return null;
		}

		/// <summary>
		/// Parses a type declarator
		/// </summary>
		/// <returns>A dummy node that contains the return type, the variable name and possible parameters of a function declaration</returns>
		DNode Declarator(ITypeDeclaration basicType,bool IsParam)
		{
			DNode ret = new DVariable() { Type=basicType, Location = la.Location };
			LastParsedObject = ret;
			ITypeDeclaration ttd = null;

			while (IsBasicType2())
			{
				if (ret.Type == null) 
					ret.Type = BasicType2();
				else { 
					ttd = BasicType2(); 
					if(ttd!=null)
						ttd.InnerDeclaration = ret.Type; 
					ret.Type = ttd; 
				}
			}
			/*
			 * Add some syntax possibilities here
			 * like
			 * int (x);
			 * int(*foo);
			 */
			#region This way of declaring function pointers is deprecated
			if (laKind == (OpenParenthesis))
			{
				Step();
				//SynErr(OpenParenthesis,"C-style function pointers are deprecated. Use the function() syntax instead."); // Only deprecated in D2
				var cd = new DelegateDeclaration() as ITypeDeclaration;
				LastParsedObject = cd;
				ret.Type = cd;
				var deleg = cd as DelegateDeclaration;

				/* 
				 * Parse all basictype2's that are following the initial '('
				 */
				while (IsBasicType2())
				{
					ttd = BasicType2();

					if (deleg.ReturnType == null) 
						deleg.ReturnType = ttd;
					else
					{
						if(ttd!=null)
							ttd.InnerDeclaration = deleg.ReturnType;
						deleg.ReturnType = ttd;
					}
				}

				/*
				 * Here can be an identifier with some optional DeclaratorSuffixes
				 */
				if (laKind != (CloseParenthesis))
				{
					if (IsParam && laKind != (Identifier))
					{
						/* If this Declarator is a parameter of a function, don't expect anything here
						 * except a '*' that means that here's an anonymous function pointer
						 */
						if (t.Kind != (Times))
							SynErr(Times);
					}
					else
					{
						if(Expect(Identifier))
							ret.Name = t.Value;

						/*
						 * Just here suffixes can follow!
						 */
						if (laKind != (CloseParenthesis))
						{
							ITemplateParameter[] _unused2 = null;
							List<INode> _unused = null;
							List<DAttribute> _unused3 = new List<DAttribute>();
							ttd = DeclaratorSuffixes(out _unused2, out _unused, _unused3);

							if (ttd != null)
							{
								ttd.InnerDeclaration = cd;
								cd = ttd;
							}
						}
					}
				}
				ret.Type = cd;
				Expect(CloseParenthesis);
			}
			#endregion
			else
			{
				// On external function declarations, no parameter names are required.
				// extern void Cfoo(HANDLE,char**);
				if (IsParam && laKind != (Identifier))
				{
					if(ret.Type!=null)
						ExpectingIdentifier = true;
					return ret;
				}

				if (Expect(Identifier))
				{
					ret.Name = t.Value;
					ret.NameLocation = t.Location;
				}
				else
				{
					// Code error! - to prevent infinite declaration loops, step one token forward anyway!
					Step();
					return ret;
				}
			}

			if (IsDeclaratorSuffix || MemberFunctionAttribute[laKind])
			{
				var dm = new DMethod();
				LastParsedObject = dm;

				// DeclaratorSuffixes
				List<INode> _Parameters;
				ttd = DeclaratorSuffixes(out ret.TemplateParameters, out _Parameters, ret.Attributes);
				if (ttd != null)
				{
					ttd.InnerDeclaration = ret.Type;
					ret.Type = ttd;
				}

				if (_Parameters == null)
					LastParsedObject = ret;

				if (_Parameters != null)
				{
					dm.AssignFrom(ret);
					dm.Parameters = _Parameters;
					foreach (var pp in dm.Parameters)
						pp.Parent = dm;
					return dm;
				}
			}

			return ret;
		}

		bool IsDeclaratorSuffix
		{
			get { return laKind == (OpenSquareBracket) || laKind == (OpenParenthesis); }
		}

		/// <summary>
		/// Note:
		/// http://www.digitalmars.com/d/2.0/declaration.html#DeclaratorSuffix
		/// The definition of a sequence of declarator suffixes is buggy here! Theoretically template parameters can be declared without a surrounding ( and )!
		/// Also, more than one parameter sequences are possible!
		/// 
		/// TemplateParameterList[opt] Parameters MemberFunctionAttributes[opt]
		/// </summary>
		ITypeDeclaration DeclaratorSuffixes(out ITemplateParameter[] TemplateParameters, out List<INode> _Parameters, List<DAttribute> attributes)
		{
			DAttribute attr = null;
			ITypeDeclaration td = null;
			TemplateParameters = null;
			_Parameters = null;

			FunctionAttributes(attributes);

			while (laKind == (OpenSquareBracket))
			{
				Step();
				var ad = new ArrayDecl() { Location=t.Location };
				LastParsedObject = ad;
				ad.InnerDeclaration = td;
				if (laKind != (CloseSquareBracket))
				{
					ad.ClampsEmpty = false;
					ITypeDeclaration keyType=null;
					var la_backup = la;
					if (!IsAssignExpression())
					{
						
						var weakType = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;
						
						keyType= ad.KeyType = Type();

						AllowWeakTypeParsing = weakType;
					}
					if (keyType == null || laKind != CloseSquareBracket)
					{
						keyType = ad.KeyType = null;
						la = la_backup;
						ad.KeyExpression = AssignExpression();
					}
				}
				Expect(CloseSquareBracket);
				ad.EndLocation = t.EndLocation;
				td = ad;
			}

			if (laKind == (OpenParenthesis))
			{
				if (IsTemplateParameterList())
				{
					TemplateParameters = TemplateParameterList();
				}
				_Parameters = Parameters(null);

				/* Is there a significant difference between storage class and memberfunctiionattribute attributes?
				while (StorageClass[laKind])
				{
					attributes.Add(attr = new DAttribute(laKind, la.Value) { Location = la.Location, EndLocation = la.EndLocation });
					LastParsedObject = attr;
					Step();
				}*/
			}

			FunctionAttributes(attributes);
			return td;
		}

		public ITypeDeclaration IdentifierList()
		{
			ITypeDeclaration td = null;

			bool notInit = false;
			do
			{
				if (notInit)
					Step();
				else
					notInit = true;

				ITypeDeclaration ttd = null;

				if (IsTemplateInstance)
					ttd = TemplateInstance();
				else if (Expect(Identifier))
					ttd = new IdentifierDeclaration(t.Value) { Location = t.Location, EndLocation = t.EndLocation };
				else if (IsEOF)
					return td;

				if (ttd != null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}
			while (laKind == Dot);

			ExpectingIdentifier = false;

			return td;
		}

		bool IsStorageClass
		{
			get
			{
				return laKind == (Abstract) ||
			laKind == (Auto) ||
			((MemberFunctionAttribute[laKind]) && Lexer.CurrentPeekToken.Kind != (OpenParenthesis)) ||
			laKind == (Deprecated) ||
			laKind == (Extern) ||
			laKind == (Final) ||
			laKind == (Override) ||
			laKind == (Scope) ||
			laKind == (Static) ||
			laKind == (Synchronized) ||
			laKind == __gshared ||
			laKind == __thread;
			}
		}

		public ITypeDeclaration Type()
		{
			var td = BasicType();

			if (IsDeclarator2())
			{
				var ttd = Declarator2();
				if (ttd != null)
					ttd.InnerMost.InnerDeclaration = td;
				td = ttd;
			}

			return td;
		}

		bool IsDeclarator2()
		{
			return IsBasicType2() || laKind == (OpenParenthesis);
		}

		/// <summary>
		/// http://www.digitalmars.com/d/2.0/declaration.html#Declarator2
		/// The next bug: Following the definition strictly, this function would end up in an endless loop of requesting another Declarator2
		/// 
		/// So here I think that a Declarator2 only consists of a couple of BasicType2's and some DeclaratorSuffixes
		/// </summary>
		/// <returns></returns>
		ITypeDeclaration Declarator2()
		{
			ITypeDeclaration td = null;
			if (laKind == (OpenParenthesis))
			{
				Step();

				td = Declarator2();
				
				if (AllowWeakTypeParsing && (td == null||(t.Kind==OpenParenthesis && laKind==CloseParenthesis) /* -- means if an argumentless function call has been made, return null because this would be an expression */|| laKind!=CloseParenthesis))
					return null;

				Expect(CloseParenthesis);

				// DeclaratorSuffixes
				if (laKind == (OpenSquareBracket))
				{
					List<INode> _unused = null;
					ITemplateParameter[] _unused2 = null;
					List<DAttribute> _unused3 = new List<DAttribute>();
					DeclaratorSuffixes(out _unused2, out _unused,_unused3);
				}
				return td;
			}

			while (IsBasicType2())
			{
				var ttd = BasicType2();
				if (AllowWeakTypeParsing && ttd == null)
					return null;

				if(ttd!=null)
					ttd.InnerDeclaration = td;
				td = ttd;
			}

			return td;
		}

		/// <summary>
		/// Parse parameters
		/// </summary>
		List<INode> Parameters(DMethod Parent)
		{
			var ret = new List<INode>();
			Expect(OpenParenthesis);

			// Empty parameter list
			if (laKind == (CloseParenthesis))
			{
				Step();
				return ret;
			}

			INode p=null;

			if (laKind != TripleDot)
			{
				p = Parameter(Parent);
				p.Parent = Parent;
				ret.Add(p);
			}

			while (laKind == (Comma))
			{
				Step();
				if (laKind == TripleDot || laKind==CloseParenthesis)
					break;
				p = Parameter(Parent);
				p.Parent = Parent;
				ret.Add(p);
			}

			/*
			 * There can be only one '...' in every parameter list
			 */
			if (laKind == TripleDot)
			{
				// If it doesn't have a comma, add a VarArgDecl to the last parameter
				bool HadComma = t.Kind == (Comma);

				Step();

				if (!HadComma && ret.Count > 0)
				{
					// Put a VarArgDecl around the type of the last parameter
					ret[ret.Count - 1].Type = new VarArgDecl(ret[ret.Count - 1].Type);
				}
				else
				{
					var dv = new DVariable();
					LastParsedObject = dv;
					dv.Type = new VarArgDecl();
					dv.Parent = Parent;
					ret.Add(dv);
				}
			}

			Expect(CloseParenthesis);
			return ret;
		}

		private INode Parameter(IBlockNode Scope = null)
		{
			var attr = new List<DAttribute>();
			var startLocation = la.Location;

			ITypeDeclaration td = null;

			while ((ParamModifiers[laKind] && laKind != InOut) || (MemberFunctionAttribute[laKind] && Lexer.CurrentPeekToken.Kind != OpenParenthesis))
			{
				Step();
				attr.Add(new DAttribute(t.Kind));
			}

			if (laKind == Auto && Lexer.CurrentPeekToken.Kind == Ref) // functional.d:595 // auto ref F fp
			{
				Step();
				Step();
				attr.Add(new DAttribute(Auto));
				attr.Add(new DAttribute(Ref));
			}

			td = BasicType();

			var ret = Declarator(td,true);
			ret.Location = startLocation;

			if (attr.Count > 0) 
				(ret as DNode).Attributes.AddRange(attr);

			// DefaultInitializerExpression
			if (laKind == (Assign))
			{
				Step();

				TrackerVariables.InitializedNode = ret;
				TrackerVariables.IsParsingInitializer = true;

				var defInit = AssignExpression(Scope);

				var dv = ret as DVariable;
				if (dv!=null)
					dv.Initializer = defInit;

				if (!IsEOF)
					TrackerVariables.IsParsingInitializer = false;
			}
			ret.EndLocation = t.EndLocation;

			return ret;
		}

		private IExpression Initializer(IBlockNode Scope = null)
		{
			Expect(Assign);

			// VoidInitializer
			if (laKind == Void)
			{
				Step();
				var ret= new VoidInitializer() { Location=t.Location,EndLocation=t.EndLocation};

				LastParsedObject = ret;
				return ret;
			}

			return NonVoidInitializer(Scope);
		}

		IExpression NonVoidInitializer(IBlockNode Scope = null)
		{
			var isParsingInitializer_backup = TrackerVariables.IsParsingInitializer;
			TrackerVariables.IsParsingInitializer = true;

			// ArrayInitializers are handled in PrimaryExpression(), whereas setting IsParsingInitializer to true is required!

			#region StructInitializer
			if (laKind == OpenCurlyBrace && IsStructInitializer)
			{
				// StructMemberInitializations
				var ae = new StructInitializer() { Location = la.Location };
				LastParsedObject = ae;
				var inits = new List<StructMemberInitializer>();

				bool IsInit = true;
				while (IsInit || laKind == (Comma))
				{
					Step();
					IsInit = false;

					// Allow empty post-comma expression IF the following token finishes the initializer expression
					// int[] a={1,2,3,4,};
					if (laKind == CloseCurlyBrace)
						break;

					// Identifier : NonVoidInitializer
					var sinit = new StructMemberInitializer { Location = la.Location };
					LastParsedObject = sinit;
					if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == Colon)
					{
						Step();
						sinit.MemberName = t.Value;
						Step();
					}

					sinit.Value = NonVoidInitializer(Scope);

					sinit.EndLocation = t.EndLocation;

					inits.Add(sinit);
				}

				Expect(DTokens.CloseCurlyBrace);

				ae.MemberInitializers = inits.ToArray();
				ae.EndLocation = t.EndLocation;

				if (!IsEOF)
					TrackerVariables.IsParsingInitializer = isParsingInitializer_backup;

				return ae;
			}
			#endregion

			var expr= AssignExpression(Scope);

			if (!IsEOF)
				TrackerVariables.IsParsingInitializer = isParsingInitializer_backup;

			return expr;
		}

		/// <summary>
		/// If there's a semicolon or a return somewhere inside the braces, it automatically is a delegate, and not a struct initializer
		/// </summary>
		bool IsStructInitializer
		{
			get
			{
				int r = 1;
				var pk = Peek(1);
				while (r > 0 && pk.Kind != DTokens.EOF && pk.Kind != DTokens.__EOF__)
				{
					switch (pk.Kind)
					{
						case DTokens.Return:
						case DTokens.Semicolon:
							return false;
						case DTokens.OpenCurlyBrace:
							r++;
							break;
						case DTokens.CloseCurlyBrace:
							r--;
							break;
					}
					pk = Peek();
				}
				return true;
			}
		}

		TypeOfDeclaration TypeOf()
		{
			Expect(Typeof);
			var md = new TypeOfDeclaration { Location = t.Location };
			LastParsedObject = md;

			if (Expect(OpenParenthesis))
			{
				if (laKind == Return)
				{
					Step();
					md.InstanceId = new TokenExpression(Return) { Location = t.Location, EndLocation = t.EndLocation };
				}
				else
					md.InstanceId = Expression();
				Expect(CloseParenthesis);
			}
			md.EndLocation = t.EndLocation;
			return md;
		}

		VectorDeclaration Vector()
		{
			var startLoc = t == null ? new CodeLocation() : t.Location;
			Expect(__vector);
			var md = new VectorDeclaration { Location = startLoc };

			if (Expect(OpenParenthesis))
			{
				LastParsedObject = md;

				if (!IsEOF)
					md.Id = Expression();

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}

			md.EndLocation = t.EndLocation;
			return md;
		}

		#endregion

		#region Attributes

		DMethod _Invariant()
		{
            var inv = new DMethod { SpecialType= DMethod.MethodType.ClassInvariant };
			LastParsedObject = inv;

			Expect(Invariant);
			inv.Location = t.Location;
			if (laKind == OpenParenthesis)
			{
				Step();
				Expect(CloseParenthesis);
			}
            if(!IsEOF)
			    inv.Body=BlockStatement();
			inv.EndLocation = t.EndLocation;
			return inv;
		}

		PragmaAttribute _Pragma()
		{
			Expect(Pragma);
			var s = new PragmaAttribute { Location = t.Location };
			LastParsedObject = s;
			if (Expect(OpenParenthesis))
			{
				if (Expect(Identifier))
					s.Identifier = t.Value;

				var l = new List<IExpression>();
				while (laKind == Comma)
				{
					Step();
					l.Add(AssignExpression());
				}
				if (l.Count > 0)
					s.Arguments = l.ToArray();

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}
			s.EndLocation = t.EndLocation;
			return s;
		}

		bool IsAttributeSpecifier()
		{
			return IsAttributeSpecifier(laKind, Lexer.CurrentPeekToken.Kind);
		}

		public static bool IsAttributeSpecifier(int laKind, int peekTokenKind=0)
		{
			return (laKind == (Extern) || laKind == (Export) || laKind == (Align) || laKind == Pragma || laKind == (Deprecated) || IsProtectionAttribute(laKind)
				|| laKind == (Static) || laKind == (Final) || laKind == (Override) || laKind == (Abstract) || laKind == (Scope) || laKind == (__gshared) || laKind==Synchronized
				|| ((laKind == (Auto) || MemberFunctionAttribute[laKind]) && (peekTokenKind != OpenParenthesis && peekTokenKind != Identifier))
				|| laKind==PropertyAttribute);
		}

		bool IsProtectionAttribute()
		{
			return IsProtectionAttribute(laKind);
		}

		public static bool IsProtectionAttribute(int laKind)
		{
			return laKind == (Public) || laKind == (Private) || laKind == (Protected) || laKind == (Extern) || laKind == (Package);
		}

		private void AttributeSpecifier(DBlockNode scope)
		{
			var attr = new DAttribute(laKind, la.Value) { Location = la.Location };
			LastParsedObject = attr;
			if (laKind == Extern && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
			{
				Step(); // Skip extern
				Step(); // Skip (

				TrackerVariables.ExpectingIdentifier = true;
				var n = "";
				while (!IsEOF && laKind != CloseParenthesis)
				{
					Step();
					n += t.ToString();

					TrackerVariables.ExpectingIdentifier = false;

					if (t.Kind == Identifier && laKind == Identifier)
						n += ' ';
				}

				attr.LiteralContent = n;

				if (!Expect(CloseParenthesis))
					return;
			}
			else if (laKind == Align && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
			{
				Step();
				Step();
				if (Expect(Literal))
					attr.LiteralContent = new IdentifierExpression(t.LiteralValue, t.LiteralFormat);

				if (!Expect(CloseParenthesis))
					return;
			}
			else if (laKind == Pragma)
				 attr=_Pragma();
			else
				Step();

			attr.EndLocation = t.EndLocation;

			if (laKind == Colon)
				LastParsedObject = null;

			//TODO: What about these semicolons after e.g. a pragma? Enlist these attributes anyway in the meta decl list?
			if (laKind != Semicolon)
				AttributeTrail(scope, attr);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="module"></param>
		/// <param name="previouslyParsedAttribute"></param>
		/// <param name="RequireDeclDef">If no colon and no open curly brace is given as lookahead, a DeclDef may be parsed otherwise, if parameter is true.</param>
		/// <returns></returns>
		AbstractMetaDeclaration AttributeTrail(DBlockNode module,DAttribute previouslyParsedAttribute, bool RequireDeclDef = false)
		{
			if (laKind == Colon)
			{
				Step();
				PushAttribute(previouslyParsedAttribute, true);

				AttributeMetaDeclarationSection metaDecl = null;
				//TODO: Put all remaining block/decl(?) attributes into the section definition..
				if(module!=null)
					module.Add(metaDecl = new AttributeMetaDeclarationSection(previouslyParsedAttribute) { EndLocation = t.EndLocation });
				return metaDecl;
			}
			else 
				PushAttribute(previouslyParsedAttribute, false);

			if (laKind == OpenCurlyBrace)
				return AttributeBlock(module);
			else
			{
				if (RequireDeclDef)
					DeclDef(module);
				return new AttributeMetaDeclaration(previouslyParsedAttribute) { EndLocation = previouslyParsedAttribute.EndLocation };
			}
		}

		void FunctionAttributes(DNode n)
		{
			if (!MemberFunctionAttribute[laKind])
				return;

			if (n.Attributes == null)
				n.Attributes = new List<DAttribute>();

			FunctionAttributes(n.Attributes);
		}

		void FunctionAttributes(List<DAttribute> attributes)
		{
			DAttribute attr = null;
			while (MemberFunctionAttribute[laKind])
			{
				attributes.Add(attr = new DAttribute(laKind, la.Value) { Location = la.Location, EndLocation = la.EndLocation });
				LastParsedObject = attr;
				Step();
			}
		}
		#endregion

		#region Expressions
		public IExpression Expression(IBlockNode Scope = null)
		{
			// AssignExpression
			var ass = AssignExpression(Scope);
			if (laKind != (Comma))
				return ass;

			/*
			 * The following is a leftover of C syntax and proably cause some errors when parsing arguments etc.
			 */
			// AssignExpression , Expression
			var ae = new Expression();
			LastParsedObject = ae;
			ae.Add(ass);
			while (laKind == (Comma))
			{
				Step();
				ae.Add(AssignExpression(Scope));
			}
			return ae;
		}

		/// <summary>
		/// This function has a very high importance because here we decide whether it's a declaration or assignExpression!
		/// </summary>
		public bool IsAssignExpression()
		{
			if (IsBasicType())
			{
				bool HadPointerDeclaration = false;

				// uint[]** MyArray;
				if (!BasicTypes[laKind])
				{
					// Skip initial dot
					if (Peek(laKind == Dot ? 2 :1).Kind != Identifier)
					{
						if (laKind == Identifier)
						{
							// Skip initial identifier list
							if (Lexer.CurrentPeekToken.Kind == Not)
							{
								Peek();
								if (Lexer.CurrentPeekToken.Kind != Is && Lexer.CurrentPeekToken.Kind != In)
								{
									if (Lexer.CurrentPeekToken.Kind == (OpenParenthesis))
										OverPeekBrackets(OpenParenthesis);
									else
										Peek();
								}
							}

							while (Lexer.CurrentPeekToken.Kind == Dot)
							{
								Peek();

								if (Lexer.CurrentPeekToken.Kind == Identifier)
								{
									Peek();

									if (Lexer.CurrentPeekToken.Kind == Not)
									{
										Peek();
										if (Lexer.CurrentPeekToken.Kind != Is && Lexer.CurrentPeekToken.Kind != In)
										{
											if (Lexer.CurrentPeekToken.Kind == (OpenParenthesis))
												OverPeekBrackets(OpenParenthesis);
											else 
												Peek();
										}
									}
								}
								else
								{
									/*
									 * If a non-identifier follows a dot, treat it as expression, not as declaration.
									 */
									Peek(1);
									return true;
								}
							}
						}
						else if ((laKind == (Typeof) || MemberFunctionAttribute[laKind]) && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
								OverPeekBrackets(OpenParenthesis);
					}
				}
				else if(Peek(1).Kind != Dot && Peek().Kind!=Identifier)
				{
					/*
					 * PrimaryExpression allows
					 * BasicType . Identifier 
					 * --> if BasicType IS int or float etc., and NO dot follows, it must be a type
					 */
					Peek(1);
					return false;
				}

				if (Lexer.CurrentPeekToken == null)
					Peek();

				// Skip basictype2's
				while (Lexer.CurrentPeekToken.Kind == Times || Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
				{
					if (Lexer.CurrentPeekToken.Kind == Times)
						HadPointerDeclaration = true;

					if (Lexer.CurrentPeekToken.Kind == OpenSquareBracket)
						OverPeekBrackets(OpenSquareBracket);
					else Peek();

					if (HadPointerDeclaration && Lexer.CurrentPeekToken.Kind == Literal) // char[a.member*8] abc; // conv.d:3278
					{
						Peek(1);
						return true;
					}
				}

				// And now, after having skipped the basictype and possible trailing basictype2's,
				// we check for an identifier or delegate declaration to ensure that there's a declaration and not an expression
				// Addition: If a times token ('*') follows an identifier list, we can assume that we have a declaration and NOT an expression!
				// Example: *a=b is an expression; a*=b is not possible (and a Multiply-Assign-Expression) - instead something like A* a should be taken...
				if (HadPointerDeclaration || 
					Lexer.CurrentPeekToken.Kind == Identifier || 
					Lexer.CurrentPeekToken.Kind == Delegate || 
					Lexer.CurrentPeekToken.Kind == Function ||

					// Also assume a declaration if no further token follows
					Lexer.CurrentPeekToken.Kind==EOF ||
					Lexer.CurrentPeekToken.Kind==__EOF__)
				{
					Peek(1);
					return false;
				}
			}
			else if (IsStorageClass)
				return false;

			Peek(1);
			return true;
		}

		public IExpression AssignExpression(IBlockNode Scope = null)
		{
			var left = ConditionalExpression(Scope);
			if (!AssignOps[laKind])
				return left;

			Step();
			var ate = new AssignExpression(t.Kind);
			LastParsedObject = ate;
			ate.LeftOperand = left;
			ate.RightOperand = AssignExpression(Scope);
			return ate;
		}

		IExpression ConditionalExpression(IBlockNode Scope = null)
		{
			var trigger = OrOrExpression(Scope);
			if (laKind != (Question))
				return trigger;

			Expect(Question);
			var se = new ConditionalExpression() { OrOrExpression = trigger };
			LastParsedObject = se;
			se.TrueCaseExpression = Expression(Scope);
			Expect(Colon);
			se.FalseCaseExpression = ConditionalExpression(Scope);
			return se;
		}

		IExpression OrOrExpression(IBlockNode Scope = null)
		{
			var left = AndAndExpression(Scope);
			if (laKind != LogicalOr)
				return left;

			Step();
			var ae = new OrOrExpression();
			LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = OrOrExpression(Scope);
			return ae;
		}

		IExpression AndAndExpression(IBlockNode Scope = null)
		{
			// Note: Due to making it easier to parse, we ignore the OrExpression-CmpExpression rule
			// -> So we only assume that there's a OrExpression

			var left = OrExpression(Scope);
			if (laKind != LogicalAnd)
				return left;

			Step();
			var ae = new AndAndExpression();
			LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = AndAndExpression(Scope);
			return ae;
		}

		IExpression OrExpression(IBlockNode Scope = null)
		{
			var left = XorExpression(Scope);
			if (laKind != BitwiseOr)
				return left;

			Step();
			var ae = new OrExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = OrExpression(Scope);
			return ae;
		}

		IExpression XorExpression(IBlockNode Scope = null)
		{
			var left = AndExpression(Scope);
			if (laKind != Xor)
				return left;

			Step();
			var ae = new XorExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = XorExpression(Scope);
			return ae;
		}

		IExpression AndExpression(IBlockNode Scope = null)
		{
			// Note: Since we ignored all kinds of CmpExpressions in AndAndExpression(), we have to take CmpExpression instead of ShiftExpression here!
			var left = CmpExpression(Scope);
			if (laKind != BitwiseAnd)
				return left;

			Step();
			var ae = new AndExpression(); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = AndExpression(Scope);
			return ae;
		}

		IExpression CmpExpression(IBlockNode Scope = null)
		{
			var left = ShiftExpression(Scope);

			OperatorBasedExpression ae = null;

			// Equality Expressions
			if (laKind == Equal || laKind == NotEqual)
				ae = new EqualExpression(laKind == NotEqual);

			// Relational Expressions
			else if (RelationalOperators[laKind])
				ae = new RelExpression(laKind);

			// Identity Expressions
			else if (laKind == Is || (laKind == Not && Peek(1).Kind == Is))
				ae = new IdendityExpression(laKind == Not);

			// In Expressions
			else if (laKind == In || (laKind == Not && Peek(1).Kind == In))
				ae = new InExpression(laKind == Not);

			else return left;

			LastParsedObject = ae;

			// Skip possible !-Token
			if (laKind == Not)
				Step();

			// Skip operator
			Step();

			ae.LeftOperand = left;
			ae.RightOperand = ShiftExpression(Scope);
			return ae;
		}

		IExpression ShiftExpression(IBlockNode Scope = null)
		{
			var left = AddExpression(Scope);
			if (!(laKind == ShiftLeft || laKind == ShiftRight || laKind == ShiftRightUnsigned))
				return left;

			Step();
			var ae = new ShiftExpression(t.Kind); LastParsedObject = ae;
			ae.LeftOperand = left;
			ae.RightOperand = ShiftExpression(Scope);
			return ae;
		}

		/// <summary>
		/// Note: Add, Multiply as well as Cat Expressions are parsed in this method.
		/// </summary>
		IExpression AddExpression(IBlockNode Scope = null)
		{
			var left = UnaryExpression(Scope);

			OperatorBasedExpression ae = null;

			switch (laKind)
			{
				case Plus:
				case Minus:
					ae = new AddExpression(laKind == Minus);
					break;
				case Tilde:
					ae = new CatExpression();
					break;
				case Times:
				case Div:
				case Mod:
					ae = new MulExpression(laKind);
					break;
				default:
					return left;
			}

			LastParsedObject = ae;

			Step();

			ae.LeftOperand = left;
			ae.RightOperand = AddExpression(Scope);
			return ae;
		}

		IExpression UnaryExpression(IBlockNode Scope = null)
		{
			// Note: PowExpressions are handled in PowExpression()

			if (laKind == (BitwiseAnd) || laKind == (Increment) ||
				laKind == (Decrement) || laKind == (Times) ||
				laKind == (Minus) || laKind == (Plus) ||
				laKind == (Not) || laKind == (Tilde))
			{
				Step();

				SimpleUnaryExpression ae = null;

				switch (t.Kind)
				{
					case BitwiseAnd:
						ae = new UnaryExpression_And();
						break;
					case Increment:
						ae = new UnaryExpression_Increment();
						break;
					case Decrement:
						ae = new UnaryExpression_Decrement();
						break;
					case Times:
						ae = new UnaryExpression_Mul();
						break;
					case Minus:
						ae = new UnaryExpression_Sub();
						break;
					case Plus:
						ae = new UnaryExpression_Add();
						break;
					case Tilde:
						ae = new UnaryExpression_Cat();
						break;
					case Not:
						ae = new UnaryExpression_Not();
						break;
				}

				LastParsedObject = ae;

				ae.Location = t.Location;

				ae.UnaryExpression = UnaryExpression(Scope);

				return ae;
			}

			// ( Type ) . Identifier
			if (laKind == OpenParenthesis)
			{
				var wkParsing = AllowWeakTypeParsing;
				AllowWeakTypeParsing = true;
				var curLA = la;
				Step();
				var td = Type();

				AllowWeakTypeParsing = wkParsing;

				if (td!=null && ((t.Kind!=OpenParenthesis && laKind == CloseParenthesis && Peek(1).Kind == Dot && Peek(2).Kind == Identifier) || 
					(IsEOF || Peek(1).Kind==EOF || Peek(2).Kind==EOF))) // Also take it as a type declaration if there's nothing following (see Expression Resolving)
				{
					Step();  // Skip to )
					Step();  // Skip to .
					Step();  // Skip to identifier

					var accExpr = new UnaryExpression_Type() { Type=td, AccessIdentifier=t.Value };

					accExpr.Location = curLA.Location;
					accExpr.EndLocation = t.EndLocation;

					return accExpr;
				}
				else
				{
					// Reset the current token with the earlier one to enable Expression parsing
					la = curLA;
					Peek(1);
				}

			}

			// CastExpression
			if (laKind == (Cast))
			{
				Step();
				var ae = new CastExpression { Location= t.Location };

				if (Expect(OpenParenthesis))
				{
					if (laKind != CloseParenthesis) // Yes, it is possible that a cast() can contain an empty type!
						ae.Type = Type();
					Expect(CloseParenthesis);
				}

				ae.UnaryExpression = UnaryExpression(Scope);

				ae.EndLocation = t.EndLocation;

				return ae;
			}

			// NewExpression
			if (laKind == (New))
				return NewExpression(Scope);

			// DeleteExpression
			if (laKind == (Delete))
			{
				Step();
				return new DeleteExpression() { UnaryExpression = UnaryExpression(Scope) };
			}


			// PowExpression
			var left = PostfixExpression(Scope);

			if (laKind != Pow)
				return left;

			Step();
			var pe = new PowExpression();
			pe.LeftOperand = left;
			pe.RightOperand = UnaryExpression(Scope);

			return pe;
		}

		IExpression NewExpression(IBlockNode Scope = null)
		{
			Expect(New);
			var startLoc = t.Location;

			IExpression[] newArgs = null;
			// NewArguments
			if (laKind == (OpenParenthesis))
			{
				Step();
				if (laKind != (CloseParenthesis))
					newArgs = ArgumentList(Scope).ToArray();
				Expect(CloseParenthesis);
			}

			/*
			 * If there occurs a class keyword here, interpretate it as an anonymous class definition
			 * http://digitalmars.com/d/2.0/expression.html#NewExpression
			 * 
			 * NewArguments ClassArguments BaseClasslist_opt { DeclDefs } 
			 * 
			 * http://digitalmars.com/d/2.0/class.html#anonymous
			 * 
				NewAnonClassExpression:
					new PerenArgumentListopt class PerenArgumentList_opt SuperClass_opt InterfaceClasses_opt ClassBody

				PerenArgumentList:
					(ArgumentList)
			 * 
			 */
			if (laKind == (Class))
			{
				Step();
				var ac = new AnonymousClassExpression(); LastParsedObject = ac;
				ac.NewArguments = newArgs;

				// ClassArguments
				if (laKind == (OpenParenthesis))
				{
					Step();
					if (laKind == (CloseParenthesis))
						Step();
					else
						ac.ClassArguments = ArgumentList(Scope).ToArray();
				}

				var anclass = new DClassLike(Class) { IsAnonymousClass=true };
				LastParsedObject = anclass;

				anclass.Name = "(Anonymous Class)";

				// BaseClasslist_opt
				if (laKind == (Colon))
					//TODO : Add base classes to expression
					anclass.BaseClasses = BaseClassList();
				// SuperClass_opt InterfaceClasses_opt
				else if (laKind != OpenCurlyBrace)
					anclass.BaseClasses = BaseClassList(false);

				ClassBody(anclass);

				ac.AnonymousClass = anclass;

				ac.Location = startLoc;
				ac.EndLocation = t.EndLocation;

				if (Scope != null)
					Scope.Add(ac.AnonymousClass);

				return ac;
			}

			// NewArguments Type
			else
			{
				var nt = BasicType();

				while (IsBasicType2())
				{
					var bt=BasicType2();

					bt.InnerDeclaration = nt;
					nt = bt;
				}

				var initExpr = new NewExpression()
				{
					NewArguments = newArgs,
					Type=nt,
					Location=startLoc
				};
				LastParsedObject = initExpr;

				var args = new List<IExpression>();

				var ad=nt as ArrayDecl;

				if((ad == null || ad.ClampsEmpty) && laKind == OpenParenthesis)
				{
					Step();
					if (laKind != CloseParenthesis)
						args = ArgumentList(Scope);
					Expect(CloseParenthesis);

					if (ad != null)
					{
						if (args.Count == 0)
						{
							SemErr(CloseParenthesis, "Size for the rightmost array dimension needed");

							initExpr.EndLocation = t.EndLocation;
							return initExpr;
						}

						while (ad != null)
						{
							if (args.Count == 0)
								break;

							ad.ClampsEmpty = false;
							ad.KeyType = null;
							ad.KeyExpression = args[args.Count - 1];

							args.RemoveAt(args.Count - 1);

							ad = ad.InnerDeclaration as ArrayDecl;
						}
					}
				}

				ad = nt as ArrayDecl;

				if (ad != null && ad.KeyExpression == null)
				{
					if (ad.KeyType != null)
						SemErr(ad.KeyType is DTokenDeclaration ? (ad.KeyType as DTokenDeclaration).Token : CloseSquareBracket, "Size of array expected, not type " + ad.KeyType);
				}

				initExpr.Arguments = args.ToArray();

				initExpr.EndLocation = t.EndLocation;
				return initExpr;
			}
		}

		public List<IExpression> ArgumentList(IBlockNode Scope = null)
		{
			var ret = new List<IExpression>();

			ret.Add(AssignExpression(Scope));

			while (laKind == (Comma))
			{
				Step();
				ret.Add(AssignExpression(Scope));
			}

			return ret;
		}

		IExpression PostfixExpression(IBlockNode Scope = null)
		{
			var curLastParsedObj = LastParsedObject;

			// PostfixExpression
			IExpression leftExpr = PrimaryExpression(Scope);
			
			if(curLastParsedObj==LastParsedObject)
				LastParsedObject = leftExpr;

			while (!IsEOF)
			{
				if (laKind == Dot)
				{
					Step();

					var e = new PostfixExpression_Access { 
						PostfixForeExpression=leftExpr
					};
					LastParsedObject = e;

					leftExpr = e;

					if (laKind == New)
						e.AccessExpression = NewExpression(Scope);
					else if (IsTemplateInstance)
						e.AccessExpression = TemplateInstance();
					else if (Expect(Identifier))
                        e.AccessExpression = new IdentifierExpression(t.Value) { Location=t.Location, EndLocation=t.EndLocation };

					e.EndLocation = t.EndLocation;
				}
				else if (laKind == Increment || laKind == Decrement)
				{
					Step();
					var e = t.Kind == Increment ? (PostfixExpression)new PostfixExpression_Increment() : new PostfixExpression_Decrement();
					LastParsedObject = e;
					e.EndLocation = t.EndLocation;					
					e.PostfixForeExpression = leftExpr;
					leftExpr = e;
				}

				// Function call
				else if (laKind == OpenParenthesis)
				{
					Step();
					var ae = new PostfixExpression_MethodCall();
					LastParsedObject = ae;
					ae.PostfixForeExpression = leftExpr;
					leftExpr = ae;

					if (laKind != CloseParenthesis)
						ae.Arguments = ArgumentList(Scope).ToArray();
					Step();
					ae.EndLocation = t.EndLocation;
				}

				// IndexExpression | SliceExpression
				else if (laKind == OpenSquareBracket)
				{
					Step();

					if (laKind != CloseSquareBracket)
					{
						var firstEx = AssignExpression(Scope);
						// [ AssignExpression .. AssignExpression ]
						if (laKind == DoubleDot)
						{
							Step();

							leftExpr = new PostfixExpression_Slice()
							{
								FromExpression = firstEx,
								PostfixForeExpression = leftExpr,
								ToExpression = AssignExpression(Scope)
							};
							LastParsedObject = leftExpr;
						}
						// [ ArgumentList ]
						else if (laKind == CloseSquareBracket || laKind == (Comma))
						{
							var args = new List<IExpression>();
							args.Add(firstEx);
							if (laKind == Comma)
							{
								Step();
								args.AddRange(ArgumentList(Scope));
							}

							leftExpr = new PostfixExpression_Index()
							{
								PostfixForeExpression = leftExpr,
								Arguments = args.ToArray()
							};
							LastParsedObject = leftExpr;
						}
					}
					else // Empty array literal = SliceExpression
					{
						leftExpr = new PostfixExpression_Slice()
						{
							PostfixForeExpression=leftExpr
						}; 
						LastParsedObject = leftExpr;
					}

					Expect(CloseSquareBracket);
					if(leftExpr is PostfixExpression)
						((PostfixExpression)leftExpr).EndLocation = t.EndLocation;
				}
				else break;
			}

			return leftExpr;
		}

		IExpression PrimaryExpression(IBlockNode Scope=null)
		{
			bool isModuleScoped = laKind == Dot;
			if (isModuleScoped)
			{
				Step();
				if (IsEOF)
				{
					LastParsedObject = new TokenExpression(Dot) { Location = t.Location, EndLocation = t.EndLocation };
					ExpectingIdentifier = true;
				}
			}

			if (laKind == __FILE__ || laKind == __LINE__)
			{
				Step();

				object id = null;

				if (t.Kind == __FILE__ && doc != null)
					id = doc.FileName;
				else if(t.Kind==__LINE__)
					id = t.Line;

				return new IdentifierExpression(id)
				{
					Location=t.Location,
					EndLocation=t.EndLocation
				};
			}

			// Dollar (== Array length expression)
			if (laKind == Dollar)
			{
				Step();
				return new TokenExpression(laKind)
				{
					Location = t.Location,
					EndLocation = t.EndLocation
				};
			}

			// TemplateInstance
			if (IsTemplateInstance)
			{
				var tix = TemplateInstance();
				if (tix != null && tix.TemplateIdentifier!=null)
					tix.TemplateIdentifier.ModuleScoped = isModuleScoped;
				return tix;
			}

			if (IsLambaExpression())
				return LambaExpression(Scope);

			// Identifier
			if (laKind == Identifier)
			{
				Step();

				return new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation,
						ModuleScoped = isModuleScoped
					};
			}

			// SpecialTokens (this,super,null,true,false,$) // $ has been handled before
			if (laKind == (This) || laKind == (Super) || laKind == (Null) || laKind == (True) || laKind == (False))
			{
				Step();
				return new TokenExpression(t.Kind)
				{
					Location = t.Location,
					EndLocation = t.EndLocation
				};
			}

			#region Literal
			if (laKind == Literal)
			{
				Step();
				var startLoc = t.Location;

				// Concatenate multiple string literals here
				if (t.LiteralFormat == LiteralFormat.StringLiteral || t.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
				{
					var a = t.Value;
					while (la.LiteralFormat == LiteralFormat.StringLiteral || la.LiteralFormat == LiteralFormat.VerbatimStringLiteral)
					{
						Step();
						a += t.Value;
					}
					return new IdentifierExpression(a, t.LiteralFormat, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
				}
				//else if (t.LiteralFormat == LiteralFormat.CharLiteral)return new IdentifierExpression(t.LiteralValue) { LiteralFormat=t.LiteralFormat,Location = startLoc, EndLocation = t.EndLocation };
				return new IdentifierExpression(t.LiteralValue, t.LiteralFormat, t.Subformat) { Location = startLoc, EndLocation = t.EndLocation };
			}
			#endregion

			#region ArrayLiteral | AssocArrayLiteral
			if (laKind == (OpenSquareBracket))
			{
				Step();
				var startLoc = t.Location;

				// Empty array literal
				if (laKind == CloseSquareBracket)
				{
					Step();
					return new ArrayLiteralExpression() {Location=startLoc, EndLocation = t.EndLocation };
				}

				/*
				 * If it's an initializer, allow NonVoidInitializers as values.
				 * Normal AssignExpressions otherwise.
				 */
				bool isInitializer = TrackerVariables.IsParsingInitializer;

				var firstExpression = isInitializer? NonVoidInitializer(Scope) : AssignExpression(Scope);

				// Associtative array
				if (laKind == Colon)
				{
					Step();

					var ae = isInitializer ?
						new ArrayInitializer { Location = startLoc } : 
						new AssocArrayExpression { Location=startLoc };
					LastParsedObject = ae;

					var firstValueExpression = isInitializer? NonVoidInitializer(Scope) : AssignExpression();

					ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(firstExpression, firstValueExpression));

					while (laKind == Comma)
					{
						Step();

						if (laKind == CloseSquareBracket)
							break;

						var keyExpr = AssignExpression();
						var valExpr=Expect(Colon) ? 
							(isInitializer? 
								NonVoidInitializer(Scope) : 
								AssignExpression(Scope)) : 
							null;

						ae.Elements.Add(new KeyValuePair<IExpression,IExpression>(keyExpr,valExpr));
					}

					Expect(CloseSquareBracket);
					ae.EndLocation = t.EndLocation;
					return ae;
				}
				else // Normal array literal
				{
					var ae = new ArrayLiteralExpression() { Location=startLoc };
					LastParsedObject = ae;
					
					ae.Elements.Add(firstExpression);

					while (laKind == Comma)
					{
						Step();
						if (laKind == CloseSquareBracket) // And again, empty expressions are allowed
							break;
						ae.Elements.Add(isInitializer? NonVoidInitializer(Scope) : AssignExpression(Scope));
					}

					Expect(CloseSquareBracket);
					ae.EndLocation = t.EndLocation;
					return ae;
				}
			}
			#endregion

			#region FunctionLiteral
			if (laKind == Delegate || laKind == Function || laKind == OpenCurlyBrace || (laKind == OpenParenthesis && IsFunctionLiteral()))
			{
				var fl = new FunctionLiteral() { Location=la.Location};
				LastParsedObject = fl;
				fl.AnonymousMethod.Location = la.Location;

				if (laKind == Delegate || laKind == Function)
				{
					Step();
					fl.LiteralToken = t.Kind;
				}

				// file.d:1248
				/*
					listdir (".", delegate bool (DirEntry * de)
					{
						auto s = std.string.format("%s : c %s, w %s, a %s", de.name,
								toUTCString (de.creationTime),
								toUTCString (de.lastWriteTime),
								toUTCString (de.lastAccessTime));
						return true;
					}
					);
				*/
				if (laKind != OpenCurlyBrace) // foo( 1, {bar();} ); -> is a legal delegate
				{
					if (!MemberFunctionAttribute[laKind] && Lexer.CurrentPeekToken.Kind == OpenParenthesis)
						fl.AnonymousMethod.Type = BasicType();
					else if (laKind != OpenParenthesis && laKind != OpenCurlyBrace)
						fl.AnonymousMethod.Type = Type();

					if (laKind == OpenParenthesis)
						fl.AnonymousMethod.Parameters = Parameters(fl.AnonymousMethod);

					while (FunctionAttribute[laKind])
					{
						Step();
						fl.AnonymousMethod.Attributes.Add(new DAttribute(t.Kind, t.Value) { Location = t.Location, EndLocation = t.EndLocation });
					}
				}

				FunctionBody(fl.AnonymousMethod);

				fl.EndLocation = t.EndLocation;

				if (Scope != null)
					Scope.Add(fl.AnonymousMethod);

				return fl;
			}
			#endregion

			#region AssertExpression
			if (laKind == (Assert))
			{
				Step();
				var startLoc = t.Location;
				Expect(OpenParenthesis);
				var ce = new AssertExpression() { Location=startLoc};
				LastParsedObject = ce;

				var exprs = new List<IExpression>();
				exprs.Add(AssignExpression());

				if (laKind == (Comma))
				{
					Step();
					exprs.Add(AssignExpression());
				}
				ce.AssignExpressions = exprs.ToArray();
				Expect(CloseParenthesis);
				ce.EndLocation = t.EndLocation;
				return ce;
			}
			#endregion

			#region MixinExpression | ImportExpression
			if (laKind == Mixin)
			{
				Step();
				var e = new MixinExpression() { Location=t.Location};
				LastParsedObject = e;
				if (Expect(OpenParenthesis))
				{
					e.AssignExpression = AssignExpression();
					Expect(CloseParenthesis);
				}
				e.EndLocation = t.EndLocation;
				return e;
			}

			if (laKind == Import)
			{
				Step();
				var e = new ImportExpression() { Location=t.Location};
				LastParsedObject = e;
				Expect(OpenParenthesis);

				e.AssignExpression = AssignExpression();

				Expect(CloseParenthesis);
				e.EndLocation = t.EndLocation;
				return e;
			}
			#endregion

			if (laKind == (Typeof))
			{
				return new TypeDeclarationExpression(TypeOf());
			}

			// TypeidExpression
			if (laKind == (Typeid))
			{
				Step();
				var ce = new TypeidExpression() { Location=t.Location};
				LastParsedObject = ce;
				Expect(OpenParenthesis);

				if (IsAssignExpression())
					ce.Expression = AssignExpression(Scope);
				else
				{
					var laBackup = la;
					AllowWeakTypeParsing = true;
					ce.Type = Type();
					AllowWeakTypeParsing = false;

					if (ce.Type == null || laKind != CloseParenthesis)
					{
						la = laBackup;
						Lexer.StartPeek();
						ce.Expression = AssignExpression();
					}
				}

				if (!Expect(CloseParenthesis) && IsEOF)
					LastParsedObject = (ISyntaxRegion)ce.Type ?? ce.Expression;

				ce.EndLocation = t.EndLocation;
				return ce;
			}

			#region IsExpression
			if (laKind == Is)
			{
				Step();
				var ce = new IsExpression() { Location=t.Location};
				LastParsedObject = ce;
				Expect(OpenParenthesis);

				if((ce.TestedType = Type())==null)
					SynErr(laKind, "In an IsExpression, either a type or an expression is required!");

				if (ce.TestedType!=null && laKind == Identifier && (Lexer.CurrentPeekToken.Kind == CloseParenthesis || Lexer.CurrentPeekToken.Kind == Equal
					|| Lexer.CurrentPeekToken.Kind == Colon))
				{
					Step();
					ce.TypeAliasIdentifier = strVal;
				}

				if (laKind == CloseParenthesis)
				{
					Step();
					ce.EndLocation = t.EndLocation;
					return ce;
				}

				if (laKind == Colon || laKind == Equal)
				{
					Step();
					ce.EqualityTest = t.Kind == Equal;
				}
				else if (laKind == CloseParenthesis)
				{
					Step();
					ce.EndLocation = t.EndLocation;
					return ce;
				}

				/*
				TypeSpecialization:
					Type
						struct
						union
						class
						interface
						enum
						function
						delegate
						super
					const
					immutable
					inout
					shared
						return
				*/

				if (ce.EqualityTest && (ClassLike[laKind] || laKind==Typedef || // typedef is possible although it's not yet documented in the syntax docs
					laKind==Enum || laKind==Delegate || laKind==Function || laKind==Super || laKind==Return ||
					((laKind==Const || laKind == Immutable || laKind == InOut || laKind == Shared) && 
					(Peek(1).Kind==CloseParenthesis || Lexer.CurrentPeekToken.Kind==Comma))))
				{
					Step();
					ce.TypeSpecializationToken = t.Kind;
				}
				else
					ce.TypeSpecialization = Type();

				if (laKind == Comma)
				{
					Step();
					ce.TemplateParameterList =
						TemplateParameterList(false);
				}

				Expect(CloseParenthesis);
				ce.EndLocation = t.EndLocation;
				return ce;
			}
			#endregion

			// ( Expression )
			if (laKind == OpenParenthesis)
			{
				Step();
				var ret = new SurroundingParenthesesExpression() {Location=t.Location };
				LastParsedObject = ret;

				ret.Expression = Expression();

				Expect(CloseParenthesis);
				ret.EndLocation = t.EndLocation;
				return ret;
			}

			// TraitsExpression
			if (laKind == (__traits))
				return TraitsExpression();

			#region BasicType . Identifier
			if (IsBasicType())
			{
				var startLoc = la.Location;

				var bt=BasicType();

				if (bt is TypeOfDeclaration && laKind!=Dot)
					return new TypeDeclarationExpression(bt);

				if (Expect(Dot) && Expect(Identifier))
                    return new PostfixExpression_Access()
                    {
                        PostfixForeExpression = new TypeDeclarationExpression(bt),
                        AccessExpression = new IdentifierExpression(t.Value) { Location=t.Location, EndLocation=t.EndLocation },
                        EndLocation = t.EndLocation
                    };

				return null;
			}
			#endregion

			SynErr(Identifier);
			Step();
			return new TokenExpression() { Location = t.Location, EndLocation = t.EndLocation };
		}

		bool IsLambaExpression()
		{
			if (laKind != OpenParenthesis)
			{
				if (laKind == Identifier && Peek(1).Kind == GoesTo)
					return true;

				return false;
			}

			Lexer.StartPeek();

			OverPeekBrackets(OpenParenthesis, false);

			return Lexer.CurrentPeekToken.Kind == GoesTo;
		}

		bool IsFunctionLiteral()
		{
			if (laKind != OpenParenthesis)
				return false;

			Lexer.StartPeek();

			OverPeekBrackets(OpenParenthesis, false);

			return Lexer.CurrentPeekToken.Kind == OpenCurlyBrace;
		}

		FunctionLiteral LambaExpression(IBlockNode Scope=null)
		{
			var fl = new FunctionLiteral { IsLambda=true };

			fl.Location = fl.AnonymousMethod.Location = la.Location;

			if (laKind == Identifier)
			{
				Step();

				var p = new DVariable { 
					Name = t.Value, 
					Location = t.Location, 
					EndLocation = t.EndLocation };

				p.Attributes.Add(new DAttribute(Auto));
				
				fl.AnonymousMethod.Parameters.Add(p);
			}
			else if (laKind == OpenParenthesis)
				fl.AnonymousMethod.Parameters = Parameters(fl.AnonymousMethod);



			if (Expect(GoesTo))
			{
				fl.AnonymousMethod.Body = new BlockStatement { Location= la.Location };

				var ae = AssignExpression(fl.AnonymousMethod);

				fl.AnonymousMethod.Body.Add(new ReturnStatement
				{
					Location = ae.Location,
					EndLocation = ae.EndLocation,
					ReturnExpression=ae
				});

				fl.AnonymousMethod.Body.EndLocation = t.EndLocation;
			}

			fl.EndLocation = fl.AnonymousMethod.EndLocation = t.EndLocation;

			if (Scope != null)
				Scope.Add(fl.AnonymousMethod);

			return fl;
		}
		#endregion

		#region Statements
		void IfCondition(IfStatement par)
		{
			var wkType = AllowWeakTypeParsing;
			AllowWeakTypeParsing = true;
			
			var la_backup = la;

			ITypeDeclaration tp = null;
			if (laKind == Auto)
			{
				Step();
				tp = new DTokenDeclaration(t.Kind) { Location=t.Location, EndLocation=t.EndLocation };
			}
			else
				tp = Type();

			AllowWeakTypeParsing = wkType;

			if (tp != null &&
				laKind == Identifier && (
				Lexer.CurrentPeekToken.Kind == Assign ||
				Lexer.CurrentPeekToken.Kind == Comma ||
				Lexer.CurrentPeekToken.Kind == CloseParenthesis))
			{
				var ifVars = new List<DVariable>();
				do
				{
					if (laKind == Comma)
						Step();

					var n = Declarator(tp, false);

					n.Location = tp.Location;

					// Initializer is optional
					if (laKind == Assign)
					{
						Step();

						if (n is DVariable)
							((DVariable)n).Initializer = Expression(par.ParentNode as IBlockNode);
					}
					n.EndLocation = t.EndLocation;
				}
				while (laKind == Comma);

				par.IfVariable = ifVars.ToArray();
			}
			else
			{
				la = la_backup;
				par.IfCondition = Expression();
			}
		}

		public bool IsStatement
		{
			get {
				return laKind == OpenCurlyBrace ||
					(laKind == Identifier && Peek(1).Kind == Colon) ||
					laKind == If || (laKind == Static && 
						(Lexer.CurrentPeekToken.Kind == If || 
						Lexer.CurrentPeekToken.Kind==Assert)) ||
					laKind == While || laKind == Do ||
					laKind == For ||
					laKind == Foreach || laKind == Foreach_Reverse ||
					(laKind == Final && Lexer.CurrentPeekToken.Kind == Switch) || laKind == Switch ||
					laKind == Case || laKind == Default ||
					laKind == Continue || laKind == Break||
					laKind==Return ||
					laKind==Goto ||
					laKind==With||
					laKind==Synchronized||
					laKind==Try||
					laKind==Throw||
					laKind==Scope||
					laKind==Asm||
					laKind==Pragma||
					laKind==Mixin||
					laKind==Version||
					laKind==Debug||
					laKind==Assert||
					laKind==Volatile
					;
			}
		}

		IStatement Statement(bool BlocksAllowed = true, bool EmptyAllowed = true, IBlockNode Scope = null, IStatement Parent=null)
		{
			if (EmptyAllowed && laKind == Semicolon)
			{
				LastParsedObject = null;
				Step();
				return null;
			}

			if (BlocksAllowed && laKind == OpenCurlyBrace)
				return BlockStatement(Scope,Parent);

			#region LabeledStatement (loc:... goto loc;)
			if (laKind == Identifier && Lexer.CurrentPeekToken.Kind == Colon)
			{
				Step();

				var ret = new LabeledStatement() { Location = t.Location, Identifier = t.Value, Parent = Parent };
				LastParsedObject = ret;
				Step();
				ret.EndLocation = t.EndLocation;

				return ret;
			}
			#endregion

			#region IfStatement
			else if (laKind == (If) || (laKind == Static && Lexer.CurrentPeekToken.Kind == If))
			{
				bool isStatic = laKind == Static;
				if (isStatic)
					Step();

				Step();

				var dbs = new IfStatement() { Location = t.Location, IsStatic = isStatic, Parent = Parent };
				LastParsedObject = dbs;
				Expect(OpenParenthesis);

				// IfCondition
				IfCondition(dbs);

				// ThenStatement
				if(Expect(CloseParenthesis))
					dbs.ThenStatement = Statement(Scope: Scope, Parent: dbs);

				// ElseStatement
				if (laKind == (Else))
				{
					Step();
					dbs.ElseStatement = Statement(Scope: Scope, Parent: dbs);
				}

				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region WhileStatement
			else if (laKind == While)
			{
				Step();

				var dbs = new WhileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				Expect(OpenParenthesis);
				dbs.Condition = Expression(Scope);
				Expect(CloseParenthesis);

				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region DoStatement
			else if (laKind == (Do))
			{
				Step();

				var dbs = new WhileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);

				Expect(While);
				Expect(OpenParenthesis);
				dbs.Condition = Expression(Scope);
				Expect(CloseParenthesis);

				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region ForStatement
			else if (laKind == (For))
			{
				Step();

				var dbs = new ForStatement { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				Expect(OpenParenthesis);

				// Initialize
				if (laKind != Semicolon)
					dbs.Initialize = Statement(false, Scope: Scope, Parent: dbs); // Against the D language theory, blocks aren't allowed here!
				else
					Step();
				// Enforce a trailing semi-colon only if there hasn't been an expression (the ; gets already skipped in there)
				//	Expect(Semicolon);

				// Test
				if (laKind != (Semicolon))
					dbs.Test = Expression(Scope);

				Expect(Semicolon);

				// Increment
				if (laKind != (CloseParenthesis))
					dbs.Increment = Expression(Scope);

				Expect(CloseParenthesis);

				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region ForeachStatement
			else if (laKind == Foreach || laKind == Foreach_Reverse)
				return ForeachStatement(Scope, Parent);
			#endregion

			#region [Final] SwitchStatement
			else if ((laKind == (Final) && Lexer.CurrentPeekToken.Kind == (Switch)) || laKind == (Switch))
			{
				var dbs = new SwitchStatement { Location = la.Location, Parent = Parent };
				LastParsedObject = dbs;
				if (laKind == (Final))
				{
					dbs.IsFinal = true;
					Step();
				}
				Step();
				Expect(OpenParenthesis);
				dbs.SwitchExpression = Expression(Scope);
				Expect(CloseParenthesis);

				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region CaseStatement
			else if (laKind == (Case))
			{
				Step();

				var dbs = new SwitchStatement.CaseStatement() { Location = la.Location, Parent = Parent };
				LastParsedObject = dbs;
				dbs.ArgumentList = Expression(Scope);

				Expect(Colon);

				// CaseRangeStatement
				if (laKind == DoubleDot)
				{
					Step();
					Expect(Case);
					dbs.LastExpression = AssignExpression();
					Expect(Colon);
				}

				var sl = new List<IStatement>();

				while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
				{
					var stmt = Statement(Scope: Scope, Parent: dbs);

					if (stmt != null)
					{
						stmt.Parent = dbs;
						sl.Add(stmt);
					}
				}

				dbs.ScopeStatementList = sl.ToArray();
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region Default
			else if (laKind == (Default))
			{
				Step();

				var dbs = new SwitchStatement.DefaultStatement()
				{
					Location = la.Location,
					Parent = Parent
				};
				LastParsedObject = dbs;

				Expect(Colon);

				var sl = new List<IStatement>();

				while (laKind != Case && laKind != Default && laKind != CloseCurlyBrace && !IsEOF)
				{
					var stmt = Statement(Scope: Scope, Parent: dbs);

					if (stmt != null)
					{
						stmt.Parent = dbs;
						sl.Add(stmt);
					}
				}

				dbs.ScopeStatementList = sl.ToArray();
				dbs.EndLocation = t.EndLocation;

				return dbs;
			}
			#endregion

			#region Continue | Break
			else if (laKind == (Continue))
			{
				Step();
				var s = new ContinueStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind == (Identifier))
				{
					Step();
					s.Identifier = t.Value;
				}
				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}

			else if (laKind == (Break))
			{
				Step();
				var s = new BreakStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind == (Identifier))
				{
					Step();
					s.Identifier = t.Value;
				}
				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region Return
			else if (laKind == (Return))
			{
				Step();
				var s = new ReturnStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				if (laKind != (Semicolon))
					s.ReturnExpression = Expression(Scope);

				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region Goto
			else if (laKind == (Goto))
			{
				Step();
				var s = new GotoStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				if (laKind == (Identifier))
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Identifier;
					s.LabelIdentifier = t.Value;
				}
				else if (laKind == Default)
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Default;
				}
				else if (laKind == (Case))
				{
					Step();
					s.StmtType = GotoStatement.GotoStmtType.Case;

					if (laKind != (Semicolon))
						s.CaseExpression = Expression(Scope);
				}

				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region WithStatement
			else if (laKind == (With))
			{
				Step();

				var dbs = new WithStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;
				Expect(OpenParenthesis);

				// Symbol
				dbs.WithExpression = Expression(Scope);

				Expect(CloseParenthesis);

				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);

				dbs.EndLocation = t.EndLocation;
				return dbs;
			}
			#endregion

			#region SynchronizedStatement
			else if (laKind == (Synchronized))
			{
				Step();
				var dbs = new SynchronizedStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = dbs;

				if (laKind == (OpenParenthesis))
				{
					Step();
					dbs.SyncExpression = Expression(Scope);
					Expect(CloseParenthesis);
				}

				dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);

				dbs.EndLocation = t.EndLocation;
				return dbs;
			}
			#endregion

			#region TryStatement
			else if (laKind == (Try))
			{
				Step();

				var s = new TryStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				s.ScopedStatement = Statement(Scope: Scope, Parent: s);

				if (!(laKind == (Catch) || laKind == (Finally)))
					SemErr(Catch, "At least one catch or a finally block expected!");

				var catches = new List<TryStatement.CatchStatement>();
				// Catches
				while (laKind == (Catch))
				{
					Step();

					var c = new TryStatement.CatchStatement() { Location = t.Location, Parent = s };
					LastParsedObject = c;

					// CatchParameter
					if (laKind == (OpenParenthesis))
					{
						Step();

						if (laKind == CloseParenthesis)
						{
							SemErr(CloseParenthesis, "Catch parameter expected, not ')'");
							Step();
						}
						else
						{
							var catchVar = new DVariable { Parent = Scope };
							LastParsedObject = catchVar;
							var tt = la; //TODO?
							catchVar.Type = BasicType();
							if (laKind != Identifier)
							{
								la = tt;
								catchVar.Type = new IdentifierDeclaration("Exception");
							}
							if(Expect(Identifier))
								catchVar.Name = t.Value;
							Expect(CloseParenthesis);

							c.CatchParameter = catchVar;
						}
					}

					c.ScopedStatement = Statement(Scope: Scope, Parent: c);
					c.EndLocation = t.EndLocation;

					catches.Add(c);
				}

				if (catches.Count > 0)
					s.Catches = catches.ToArray();

				if (laKind == (Finally))
				{
					Step();

					var f = new TryStatement.FinallyStatement() { Location = t.Location, Parent = Parent };
					LastParsedObject = f;

					f.ScopedStatement = Statement();
					f.EndLocation = t.EndLocation;

					s.FinallyStmt = f;
				}

				s.EndLocation = t.EndLocation;
				return s;
			}
			#endregion

			#region ThrowStatement
			else if (laKind == (Throw))
			{
				Step();
				var s = new ThrowStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;

				s.ThrowExpression = Expression(Scope);
				Expect(Semicolon);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region ScopeGuardStatement
			else if (laKind == DTokens.Scope)
			{
				Step();

				if (laKind == OpenParenthesis)
				{
					var s = new ScopeGuardStatement() { Location = t.Location, Parent = Parent };
					LastParsedObject = s;

					Step();

					if (Expect(Identifier) && t.Value != null) // exit, failure, success
						s.GuardedScope = t.Value.ToLower();

					if (Expect(CloseParenthesis))
						TrackerVariables.ExpectingIdentifier = false;

					if (!IsEOF)
						s.ScopedStatement = Statement(Scope: Scope, Parent: s);

					s.EndLocation = t.EndLocation;
					return s;
				}
				else
					PushAttribute(new DAttribute(DTokens.Scope), false);
			}
			#endregion

			#region AsmStmt
			else if (laKind == Asm)
				return AsmStatement(Parent);
			#endregion

			#region PragmaStatement
			else if (laKind == (Pragma))
			{
				var s = new PragmaStatement { Location = la.Location };

				s.Pragma = _Pragma();
				s.Parent = Parent;

				s.ScopedStatement = Statement(Scope: Scope, Parent: s);
				s.EndLocation = t.EndLocation;
				return s;
			}
			#endregion

			#region MixinStatement
			//TODO: Handle this one in terms of adding it to the node structure
			else if (laKind == (Mixin))
			{
				if (Peek(1).Kind == OpenParenthesis)
					return MixinDeclaration();
				else
					return TemplateMixin();
			}
			#endregion

			#region Conditions
			if (laKind == Debug)
				return DebugStatement(Scope, Parent);

			if (laKind == Version)
				return VersionStatement(Scope, Parent);
			#endregion

			#region (Static) AssertExpression
			else if (laKind == Assert || (laKind == Static && Lexer.CurrentPeekToken.Kind == Assert))
			{
				var s = new AssertStatement() { Location = la.Location, IsStatic = laKind == Static, Parent = Parent };
				LastParsedObject = s;

				if (s.IsStatic)
					Step();

				Step();

				if (Expect(OpenParenthesis))
				{
					s.AssertedExpression = Expression(Scope);
					Expect(CloseParenthesis);
					Expect(Semicolon);
				}
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			#region D1: VolatileStatement
			else if (laKind == Volatile)
			{
				Step();
				var s = new VolatileStatement() { Location = t.Location, Parent = Parent };
				LastParsedObject = s;
				s.ScopedStatement = Statement(Scope: Scope, Parent: s);
				s.EndLocation = t.EndLocation;

				return s;
			}
			#endregion

			// ImportDeclaration
			else if (laKind == Import)
				return ImportDeclaration();

			else if (!(ClassLike[laKind] || BasicTypes[laKind] || laKind == Enum || Modifiers[laKind] || laKind == PropertyAttribute || laKind == Alias || laKind == Typedef) && IsAssignExpression())
			{
				var s = new ExpressionStatement() { Location = la.Location, Parent = Parent };

				if (!IsEOF)
					LastParsedObject = s;
				// a==b, a=9; is possible -> Expressions can be there, not only single AssignExpressions!
				s.Expression = Expression(Scope);

				if (Expect(Semicolon))
					LastParsedObject = null;

				s.EndLocation = t.EndLocation;
				return s;
			}
			else
			{
				var s = new DeclarationStatement() { Location = la.Location, Parent = Parent };
				LastParsedObject = s;
				s.Declarations = Declaration(Scope);

				s.EndLocation = t.EndLocation;
				return s;
			}
		}

		ForeachStatement ForeachStatement(IBlockNode Scope,IStatement Parent)
		{
			Step();

			var dbs = new ForeachStatement() { 
				Location = t.Location, 
				IsReverse = t.Kind == Foreach_Reverse, 
				Parent = Parent 
			};

			LastParsedObject = dbs;
			Expect(OpenParenthesis);

			var tl = new List<DVariable>();

			bool init = true;
			while (init || laKind == (Comma))
			{
				if (!init) Step();
				init = false;

				var forEachVar = new DVariable();
				LastParsedObject = forEachVar;
				forEachVar.Location = la.Location;

				if (laKind == (Ref))
				{
					Step();
					forEachVar.Attributes.Add(new DAttribute(Ref));
				}
				if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Semicolon) || Lexer.CurrentPeekToken.Kind == Comma))
				{
					Step();
					forEachVar.Name = t.Value;
				}
				else
				{
					forEachVar.Type = Type();
					if (laKind == Identifier)
					{
						Expect(Identifier);
						forEachVar.Name = t.Value;
					}
				}
				forEachVar.EndLocation = t.EndLocation;

				tl.Add(forEachVar);
			}
			dbs.ForeachTypeList = tl.ToArray();

			Expect(Semicolon);
			dbs.Aggregate = Expression(Scope);

			// ForeachRangeStatement
			if (laKind == DoubleDot)
			{
				Step();
				dbs.UpperAggregate = Expression();
			}

			Expect(CloseParenthesis);

			dbs.ScopedStatement = Statement(Scope: Scope, Parent: dbs);
			dbs.EndLocation = t.EndLocation;

			return dbs;
		}

		AsmStatement AsmStatement(IStatement Parent)
		{
			Step();
			var s = new AsmStatement() { Location = t.Location, Parent = Parent };
			LastParsedObject = s;

			Expect(OpenCurlyBrace);

			var l = new List<string>();
			var curInstr = "";
			while (!IsEOF && laKind != (CloseCurlyBrace))
			{
				if (laKind == Semicolon)
				{
					l.Add(curInstr.Trim());
					curInstr = "";
				}
				else
					curInstr += laKind == Identifier ? la.Value : DTokens.GetTokenString(laKind);

				Step();
			}

			Expect(CloseCurlyBrace);
			s.EndLocation = t.EndLocation;
			return s;
		}

		ConditionStatement.DebugStatement DebugStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();
			var s = new ConditionStatement.DebugStatement() { Location = t.Location, Parent = Parent };
			LastParsedObject = s;

			if (laKind == OpenParenthesis)
			{
				Step();
				if (laKind == Identifier || laKind == Literal)
				{
					Step();
					if (laKind == Literal)
						s.DebugIdentifierOrLiteral = t.LiteralValue;
					else
						s.DebugIdentifierOrLiteral = t.Value;
				}
				else
					SynErr(t.Kind, "Identifier or Literal expected, " + DTokens.GetTokenString(t.Kind) + " found");

				Expect(CloseParenthesis);
			}

			s.ScopedStatement = Statement(Scope: Scope, Parent: s);

			if (laKind == Else)
			{
				Step();
				s.ElseStatement = Statement(Scope: Scope, Parent: s);
			}

			s.EndLocation = t.EndLocation;
			return s;
		}

		ConditionStatement.VersionStatement VersionStatement(IBlockNode Scope, IStatement Parent)
		{
			Step();
			var s = new ConditionStatement.VersionStatement() { Location = t.Location, Parent = Parent };

			if (laKind == OpenParenthesis)
			{
				Step();
				if (laKind == Identifier || laKind == Literal || laKind == Unittest)
				{
					Step();
					if (laKind == Unittest)
						s.VersionIdentifierOrLiteral = "unittest";
					else if (laKind == Literal)
						s.VersionIdentifierOrLiteral = t.LiteralValue;
					else
						s.VersionIdentifierOrLiteral = t.Value;
				}
				else
					SynErr(t.Kind, "Identifier or Literal expected, " + DTokens.GetTokenString(t.Kind) + " found");

				Expect(CloseParenthesis);
			}

			s.ScopedStatement = Statement(Parent: s);

			if (laKind == Else)
			{
				Step();
				s.ElseStatement = Statement(Parent: s);
			}

			s.EndLocation = t.EndLocation;
			return s;
		}

		public BlockStatement BlockStatement(INode ParentNode=null, IStatement Parent=null)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = "";

			var bs = new BlockStatement() { Location=la.Location, ParentNode=ParentNode, Parent=Parent};
			LastParsedObject = bs;

			if (Expect(OpenCurlyBrace))
			{
				if (ParseStructureOnly && laKind != CloseCurlyBrace)
				{
					Lexer.SkipCurrentBlock();
					laKind = la.Kind;
				}
				else
				{
					while (!IsEOF && laKind != (CloseCurlyBrace))
					{
						var prevLocation = la.Location;
						var s = Statement(Scope: ParentNode as IBlockNode);

						// Avoid infinite loops -- hacky?
						if (prevLocation == la.Location)
						{
							Step();
							break;
						}

						bs.Add(s);
					}
				}
				if (Expect(CloseCurlyBrace))
					LastParsedObject = null;

				if (!IsEOF)
					LastParsedObject = bs;
			}
			if(t!=null)
				bs.EndLocation = t.EndLocation;

			PreviousComment = OldPreviousCommentString;
			return bs;
		}
		#endregion

		#region Structs & Unions
		private INode AggregateDeclaration(INode Parent)
		{
			var classType = laKind;
			if (!(classType == Union || classType == Struct))
				SynErr(t.Kind, "union or struct required");
			Step();

			var ret = new DClassLike(t.Kind) { 
				Location = t.Location, 
				Description = GetComments(),
                ClassType=classType,
				Parent=Parent
			};
			LastParsedObject = ret;

			ApplyAttributes(ret);

			// Allow anonymous structs&unions
			if (laKind == Identifier)
			{
				Expect(Identifier);
				ret.Name = t.Value;
				ret.NameLocation = t.Location;
			}

			if (laKind == (Semicolon))
			{
				Step();
				return ret;
			}

			// StructTemplateDeclaration
			if (laKind == (OpenParenthesis))
			{
				ret.TemplateParameters = TemplateParameterList();

				// Constraint[opt]
				if (laKind == (If))
					Constraint();
			}

			ClassBody(ret);

			return ret;
		}
		#endregion

		#region Classes
		private INode ClassDeclaration(INode Parent)
		{
			Expect(Class);

			var dc = new DClassLike(Class) { 
				Location = t.Location,
				Description=GetComments(),
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			Expect(Identifier);
			dc.Name = t.Value;
			dc.NameLocation = t.Location;

			if (laKind == (OpenParenthesis))
			{
				dc.TemplateParameters = TemplateParameterList(true);

				// Constraints
				if (laKind == If)
				{
					Step();
					Expect(OpenParenthesis);

					dc.TemplateConstraint = Expression();

					Expect(CloseParenthesis);
				}
			}

			if (laKind == (Colon))
				dc.BaseClasses = BaseClassList();

			ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}

		private List<ITypeDeclaration> BaseClassList(bool ExpectColon=true)
		{
			if (ExpectColon) Expect(Colon);

			var ret = new List<ITypeDeclaration>();

			bool init = true;
			while (init || laKind == (Comma))
			{
				if (!init) Step();
				init = false;
				if (IsProtectionAttribute() && laKind != (Protected))
					Step();

				var ids=IdentifierList();
				if (ids != null)
					ret.Add(ids);
			}
			return ret;
		}

		public void ClassBody(DBlockNode ret,bool KeepBlockAttributes=false,bool UpdateBoundaries=true)
		{
			var OldPreviousCommentString = PreviousComment;
			PreviousComment = "";

			if (Expect(OpenCurlyBrace))
			{
				var stk_backup = BlockAttributes;

				if(!KeepBlockAttributes)
					BlockAttributes = new Stack<DAttribute>();

				if(UpdateBoundaries)
					ret.BlockStartLocation = t.Location;

				while (!IsEOF && laKind != (CloseCurlyBrace))
				{
					DeclDef(ret);
				}

				if (!IsEOF)
					LastParsedObject = ret;

				if (Expect(CloseCurlyBrace))
					LastParsedObject = null;

				if(UpdateBoundaries)
					ret.EndLocation = t.EndLocation;

				if(!KeepBlockAttributes)
					BlockAttributes = stk_backup;
			}

			PreviousComment = OldPreviousCommentString;

			if(ret!=null)
				ret.Description += CheckForPostSemicolonComment();
		}

		INode Constructor(DBlockNode scope,bool IsStruct)
		{
			Expect(This);
			var dm = new DMethod(){
				Parent = scope,
				SpecialType = DMethod.MethodType.Constructor,
				Location = t.Location,
				Name = DMethod.ConstructorIdentifier
			};
			dm.Description = GetComments();
			LastParsedObject = dm;

			if (IsStruct && Lexer.CurrentPeekToken.Kind == (This) && laKind == (OpenParenthesis))
			{
				var dv = new DVariable();
				LastParsedObject = dv;
				dv.Parent = dm;
				dv.Name = DMethod.ConstructorIdentifier;
				dm.Parameters.Add(dv);
				Step();
				Step();
				Expect(CloseParenthesis);
			}
			else
			{
				if (IsTemplateParameterList())
					dm.TemplateParameters = TemplateParameterList();

				dm.Parameters = Parameters(dm);
			}

			// handle post argument attributes
			FunctionAttributes(dm);

			if (!IsEOF)
				LastParsedObject = dm;

			if (laKind == If)
				Constraint();

			// handle post argument attributes
			FunctionAttributes(dm);

			if(IsFunctionBody)
				FunctionBody(dm);
			return dm;
		}

		INode Destructor()
		{
			Expect(Tilde);
			Expect(This);
			var dm = new DMethod();
			LastParsedObject = dm;

			dm.SpecialType = DMethod.MethodType.Destructor;
			dm.Location = Lexer.LastToken.Location;
			dm.Name = "~this";

			if (IsTemplateParameterList())
				dm.TemplateParameters = TemplateParameterList();

			dm.Parameters = Parameters(dm);

			if (laKind == (If))
				Constraint();

			FunctionBody(dm);
			return dm;
		}
		#endregion

		#region Interfaces
		private IBlockNode InterfaceDeclaration(INode Parent)
		{
			Expect(Interface);
			var dc = new DClassLike() { 
				Location = t.Location, 
				Description = GetComments(),
                ClassType= DTokens.Interface,
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			Expect(Identifier);
			dc.Name = t.Value;
			dc.NameLocation = t.Location;

			if (laKind == (OpenParenthesis))
				dc.TemplateParameters = TemplateParameterList();

			if (laKind == (If))
				dc.TemplateConstraint=Constraint();

			if (laKind == (Colon))
				dc.BaseClasses = BaseClassList();

			// Empty interfaces are allowed
			if (laKind == Semicolon)
				Step();
			else
				ClassBody(dc);

			dc.EndLocation = t.EndLocation;
			return dc;
		}

		IExpression Constraint()
		{
			IExpression ret;
			Expect(If);
			Expect(OpenParenthesis);
			ret=Expression();
			Expect(CloseParenthesis);
			return ret;
		}
		#endregion

		#region Enums
		private INode[] EnumDeclaration(INode Parent)
		{
			Expect(Enum);
			var ret = new List<INode>();

			var mye = new DEnum() { Location = t.Location, Description = GetComments(), Parent=Parent };
			LastParsedObject = mye;

			ApplyAttributes(mye);

			if (IsBasicType() && laKind != Identifier)
				mye.Type = Type();
			else if (laKind == Auto)
			{
				Step();
				mye.Attributes.Add(new DAttribute(Auto));
			}

			if (laKind == (Identifier))
			{
				// Normal enum identifier
				if (Lexer.CurrentPeekToken.Kind == (Assign) || // enum e = 1234;
					Lexer.CurrentPeekToken.Kind == (OpenCurlyBrace) || // enum e { A,B,C, }
					Lexer.CurrentPeekToken.Kind == (Semicolon) || // enum e;
					Lexer.CurrentPeekToken.Kind == Colon) // enum e : uint {..}
				{
					Step();
					mye.Name = t.Value;
					mye.NameLocation = t.Location;
				}
				else
				{
					if(mye.Type == null)
						mye.Type = Type();

					if (Expect(Identifier))
					{
						mye.Name = t.Value;
						mye.NameLocation = t.Location;
					}
				}
			}

			if (IsDeclaratorSuffix)
			{
				var _unused = new List<INode>();
				var bt2=DeclaratorSuffixes(out mye.TemplateParameters, out _unused, mye.Attributes);

				if (bt2 != null)
				{
					bt2.InnerDeclaration = mye.Type;
					mye.Type = bt2;
				}
			}

			// Enum inhertance type
			if (laKind == (Colon))
			{
				Step();
				mye.Type = Type();
			}

			// Variables with 'enum' as base type
			if (laKind == (Assign) || laKind == (Semicolon))
			{
				do
				{
					var enumVar = new DVariable();
					LastParsedObject = enumVar;

					enumVar.AssignFrom(mye);

					enumVar.Attributes.Add(new DAttribute(Enum));
					if (mye.Type != null)
						enumVar.Type = mye.Type;
					else
						enumVar.Type = new DTokenDeclaration(Enum);

					if (laKind == (Comma))
					{
						Step();
						Expect(Identifier);
						enumVar.Name = t.Value;
						enumVar.NameLocation = t.Location;
					}

					if (laKind == (Assign))
					{
						//Step(); -- expected by initializer
						enumVar.Initializer = Initializer(); // Seems to be specified wrongly - theoretically there must be an AssignExpression();
					}
					enumVar.EndLocation = t.Location;
					ret.Add(enumVar);
				}
				while (laKind == Comma);

				Expect(Semicolon);
			}
			else
			{
				// Normal enum block
				Expect(OpenCurlyBrace);

				var OldPreviousComment = PreviousComment;
				PreviousComment = "";
				mye.BlockStartLocation = t.Location;

				bool init = true;
				// While there are commas, loop through
				while ((init && laKind != (Comma)) || laKind == (Comma))
				{
					if (!init) Step();
					init = false;

					if (laKind == CloseCurlyBrace) break;

					var ev = new DEnumValue() { Location = la.Location, Description = GetComments(), Parent = mye };
					LastParsedObject = ev;

					if (laKind == Identifier && (
						Lexer.CurrentPeekToken.Kind == Assign ||
						Lexer.CurrentPeekToken.Kind == Comma || 
						Lexer.CurrentPeekToken.Kind == CloseCurlyBrace))
					{
						Step();
						ev.Name = t.Value;
						ev.NameLocation = t.Location;
					}
					else
					{
						ev.Type = Type();
						Expect(Identifier);
						ev.Name = t.Value;
						ev.NameLocation = t.Location;
					}

					if (laKind == (Assign))
					{
						Step();
						ev.Initializer = AssignExpression();
					}

					ev.EndLocation = t.EndLocation;
					ev.Description += CheckForPostSemicolonComment();

					mye.Add(ev);
				}
				Expect(CloseCurlyBrace);
				PreviousComment = OldPreviousComment;

				mye.EndLocation = t.EndLocation;
				
				// Important: Add the enum block, whereas it CAN be unnamed, to the return array
				ret.Add(mye);
			}

			mye.Description += CheckForPostSemicolonComment();

			return ret.ToArray();
		}
		#endregion

		#region Functions
		bool IsFunctionBody { get { return laKind == In || laKind == Out || laKind == Body || laKind == OpenCurlyBrace; } }

		void FunctionBody(DMethod par)
		{
			if (laKind == Semicolon) // Abstract or virtual functions
			{
				Step();
				par.Description += CheckForPostSemicolonComment();
				par.EndLocation = t.EndLocation;
				return;
			}

			while (
				(laKind == In && par.In == null) ||
				(laKind == Out && par.Out == null))
			{
				if (laKind == In)
				{
					Step();

					par.In = BlockStatement(par);
				}

				if (laKind == Out)
				{
					Step();

					if (laKind == OpenParenthesis)
					{
						Step();
						if (Expect(Identifier))
						{
							par.OutResultVariable = new IdentifierDeclaration(t.Value) { Location=t.Location, EndLocation=t.EndLocation };
						}
						Expect(CloseParenthesis);
					}

					par.Out = BlockStatement(par);
				}
			}

			// Although there can be in&out constraints, there doesn't have to be a direct body definition. Used on abstract class/interface methods.
			if (laKind == Body)
				Step();

			if ((par.In==null && par.Out==null) || 
				laKind == OpenCurlyBrace)
			{
				par.Body = BlockStatement(par);
			}

			par.EndLocation = t.EndLocation;
		}
		#endregion

		#region Templates
		/*
         * American beer is like sex on a boat - Fucking close to water;)
         */

		private INode TemplateDeclaration(INode Parent)
		{
			var startLoc = la.Location;
			// TemplateMixinDeclaration
			bool isTemplateMixinDecl = laKind == Mixin;
			if (isTemplateMixinDecl)
				Step();
			Expect(Template);
			var dc = new DClassLike(Template) {
				Description=GetComments(),
				Location=startLoc,
				Parent=Parent
			};
			LastParsedObject = dc;

			ApplyAttributes(dc);

			if (isTemplateMixinDecl)
				dc.Attributes.Add(new DAttribute(Mixin));

			if (Expect(Identifier))
			{
				dc.Name = t.Value;
				dc.NameLocation = t.Location;
			}

			dc.TemplateParameters = TemplateParameterList();

			if (laKind == (If))
				dc.TemplateConstraint=Constraint();

			// [Must not contain a base class list]

			ClassBody(dc);

			return dc;
		}

		TemplateMixin TemplateMixin()
		{
			// mixin TemplateIdentifier !( TemplateArgumentList ) MixinIdentifier ;
			//							|<--			optional			 -->|
			var r = new TemplateMixin();
			LastParsedObject = r;
			ITypeDeclaration preQualifier = null;

			Expect(Mixin);
			r.Location = t.Location;
			
			if (laKind == Dot)
			{
				Step();
			}
			else if(laKind!=Identifier)
			{
				if (laKind == Typeof)
				{
					preQualifier=TypeOf();
				}
				else if (laKind == __vector)
				{
					//TODO: Parse vectors(?)
				}

				Expect(Dot);
			}

			r.Qualifier= IdentifierList();
			if (r.Qualifier != null)
				r.Qualifier.InnerMost.InnerDeclaration = preQualifier;
			else
				r.Qualifier = preQualifier;

			// MixinIdentifier
			if (laKind == Identifier)
			{
				Step();
				r.MixinId = t.Value;
			}

			Expect(Semicolon);
			r.EndLocation = t.EndLocation;

			return r;
		}

		/// <summary>
		/// Be a bit lazy here with checking whether there're templates or not
		/// </summary>
		private bool IsTemplateParameterList()
		{
			Lexer.StartPeek();
			var pk = la;
			int r = 0;
			while (r >= 0 && pk.Kind != EOF && pk.Kind != __EOF__)
			{
				if (pk.Kind == OpenParenthesis)
					r++;
				else if (pk.Kind == CloseParenthesis)
				{
					r--;
					if (r <= 0)
						return Peek().Kind == OpenParenthesis;
				}
				pk = Peek();
			}
			return false;
		}

		private ITemplateParameter[] TemplateParameterList()
		{
			return TemplateParameterList(true);
		}

		private ITemplateParameter[] TemplateParameterList(bool MustHaveSurroundingBrackets)
		{
			if (MustHaveSurroundingBrackets) Expect(OpenParenthesis);

			var ret = new List<ITemplateParameter>();

			if (laKind == (CloseParenthesis))
			{
				Step();
				return ret.ToArray();
			}

			bool init = true;
			while (init || laKind == (Comma))
			{
				if (!init) Step();
				init = false;

				ret.Add(TemplateParameter());
			}

			if (MustHaveSurroundingBrackets) Expect(CloseParenthesis);

			return ret.ToArray();
		}

		ITemplateParameter TemplateParameter()
		{
			// TemplateThisParameter
			if (laKind == (This))
			{
				Step();

				var ret= new TemplateThisParameter()
				{
					Location=t.Location,
					FollowParameter=TemplateParameter(),
					EndLocation=t.EndLocation
				};
				LastParsedObject = ret;
				return ret;
			}

			// TemplateTupleParameter
			if (laKind == (Identifier) && Lexer.CurrentPeekToken.Kind == TripleDot)
			{
				Step();
				var startLoc = t.Location;
				var id = t.Value;
				Step();

				var ret=new TemplateTupleParameter() { Name=id, Location=startLoc, EndLocation=t.EndLocation};
				LastParsedObject = ret;
				return ret;
			}

			// TemplateAliasParameter
			if (laKind == (Alias))
			{
				Step();
				var al = new TemplateAliasParameter() { Location=t.Location };
				LastParsedObject = al;

				Expect(Identifier);

				al.Name = t.Value;

				// TODO?:
				// alias BasicType Declarator TemplateAliasParameterSpecialization_opt TemplateAliasParameterDefault_opt

				// TemplateAliasParameterSpecialization
				if (laKind == (Colon))
				{
					Step();

					AllowWeakTypeParsing=true;
					al.SpecializationType = Type();
					AllowWeakTypeParsing=false;

					if (al.SpecializationType==null)
						al.SpecializationExpression = ConditionalExpression();
				}

				// TemplateAliasParameterDefault
				if (laKind == (Assign))
				{
					Step();

					AllowWeakTypeParsing=true;
					al.DefaultType = Type();
					AllowWeakTypeParsing=false;

					if (al.DefaultType==null)
						al.DefaultExpression = ConditionalExpression();
				}
				al.EndLocation = t.EndLocation;
				return al;
			}

			// TemplateTypeParameter
			if (laKind == (Identifier) && (Lexer.CurrentPeekToken.Kind == (Colon) || Lexer.CurrentPeekToken.Kind == (Assign) || Lexer.CurrentPeekToken.Kind == (Comma) || Lexer.CurrentPeekToken.Kind == (CloseParenthesis)))
			{
				Expect(Identifier);
				var tt = new TemplateTypeParameter() { Location=t.Location };
				LastParsedObject = tt;

				tt.Name = t.Value;

				if (laKind == Colon)
				{
					Step();
					tt.Specialization = Type();
				}

				if (laKind == Assign)
				{
					Step();
					tt.Default = Type();
				}
				tt.EndLocation = t.EndLocation;
				return tt;
			}

			// TemplateValueParameter
			var tv = new TemplateValueParameter() { Location=la.Location };
			LastParsedObject = tv;
				
			var bt = BasicType();
			var dv = Declarator(bt,false);

			tv.Type = dv.Type;
			tv.Name = dv.Name;

			if (laKind == (Colon))
			{
				Step();
				tv.SpecializationExpression = ConditionalExpression();
			}

			if (laKind == (Assign))
			{
				Step();
				tv.DefaultExpression = AssignExpression();
			}
			tv.EndLocation = t.EndLocation;
			return tv;
		}

		bool IsTemplateInstance
		{
			get {
				return laKind == Identifier && Peek(1).Kind == Not && !(Peek(2).Kind == Is || Lexer.CurrentPeekToken.Kind == In);
			}
		}

		public TemplateInstanceExpression TemplateInstance()
		{
			if (!Expect(Identifier))
				return null;

			var td = new TemplateInstanceExpression() { 
				TemplateIdentifier = new IdentifierDeclaration(t.Value) 
				{ 
					Location=t.Location,
					EndLocation=t.EndLocation 
				}, 
				Location = t.Location 
			};
			LastParsedObject = td;

			var args = new List<IExpression>();

			if (!Expect(Not))
				return td;

			if (laKind == (OpenParenthesis))
			{
				Step();

				if (laKind != CloseParenthesis)
				{
					bool init = true;
					while (laKind == Comma || init)
					{
						if (!init) Step();
						init = false;

						if (IsEOF)
						{
							args.Add(new TokenExpression(DTokens.INVALID) { Location= la.Location, EndLocation=la.EndLocation });
							break;
						}

						var la_Backup = la;

						bool wp = AllowWeakTypeParsing;
						AllowWeakTypeParsing = true;

						var typeArg = Type();

						AllowWeakTypeParsing = wp;

						if (typeArg != null && (laKind == CloseParenthesis || laKind==Comma))
							args.Add(new TypeDeclarationExpression(typeArg));
						else
						{
							la = la_Backup;
							Peek(1);

							args.Add(AssignExpression());
						}
					}
				}
				Expect(CloseParenthesis);
			}
			else
			{
				Step();

				/*
				 * TemplateSingleArgument: 
				 *		Identifier 
				 *		BasicTypeX 
				 *		CharacterLiteral 
				 *		StringLiteral 
				 *		IntegerLiteral 
				 *		FloatLiteral 
				 *		true 
				 *		false 
				 *		null 
				 *		__FILE__ 
				 *		__LINE__
				 */

				IExpression arg= null;

				if (t.Kind == Literal)
					arg = new IdentifierExpression(t.LiteralValue, LiteralFormat.Scalar)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (t.Kind == Identifier)
					arg = new IdentifierExpression(t.Value)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (BasicTypes[t.Kind])
					arg = new TypeDeclarationExpression(new DTokenDeclaration(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					});
				else if (
					t.Kind == True ||
					t.Kind == False ||
					t.Kind == Null ||
					t.Kind == __FILE__ ||
					t.Kind == __LINE__)
					arg = new TokenExpression(t.Kind)
					{
						Location = t.Location,
						EndLocation = t.EndLocation
					};
				else if (IsEOF)
				{
					ExpectingIdentifier = false;
					return td;
				}

				args.Add(arg);
			}
			td.Arguments = args.ToArray();
			td.EndLocation = t.EndLocation;
			return td;
		}
		#endregion

		#region Traits
		IExpression TraitsExpression()
		{
			Expect(__traits);
			var ce = new TraitsExpression() { Location=t.Location};
			LastParsedObject = ce;
			if(Expect(OpenParenthesis))
			{
				if(Expect(Identifier))
					ce.Keyword = t.Value;

				var al = new List<TraitsArgument>();

				while (laKind == Comma)
				{
					Step();

					if (IsAssignExpression())
						al.Add(new TraitsArgument(){AssignExpression= AssignExpression()});
					else
						al.Add(new TraitsArgument(){Type= Type()});
				}

				if (Expect(CloseParenthesis))
					TrackerVariables.ExpectingIdentifier = false;
			}
			ce.EndLocation = t.EndLocation;
			return ce;
		}
		#endregion
	}

}