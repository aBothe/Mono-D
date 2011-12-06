using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using D_Parser.Dom;

namespace D_Parser.Dom
{
	/*
    /// <summary>
    /// (De-)Serializes node trees to binary files
    /// </summary>
    public class BinaryNodeStorage
    {
        #region General
        public readonly BinaryWriter BinWriter;
        public readonly BinaryReader BinReader;

        public readonly bool IsWriting;

        public const uint ModuleInitializer = (uint)('D') | ('M' << 8) | ('o' << 16) | ('d' << 24);
        public const uint NodeInitializer = (uint)('N') | ('o' << 8) | ('d' << 16) | ('e' << 24);

        public BinaryNodeStorage(string file,bool OpenForWriting)
        {
            IsWriting=OpenForWriting;

            if(IsWriting)
            {
                var fs = new FileStream(file, FileMode.Create, FileAccess.Write);
                BinWriter = new BinaryWriter(fs);
            }
            else
            {
                var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                BinReader = new BinaryReader(fs);
            }
        }

        public BinaryNodeStorage(Stream stream, bool OpenForWriting)
        {
            IsWriting = OpenForWriting;

            if (IsWriting)
                BinWriter = new BinaryWriter(stream);
            else
                BinReader = new BinaryReader(stream);
        }

        public void Close()
        {
            if(BinReader!=null)
            {
                BinReader.Close();
            }

            if (BinWriter != null)
            {
                BinWriter.Flush();
                BinWriter.Close();
            }
        }

        public void WriteString(string s) { WriteString(s, false); }
        public string ReadString() { return ReadString(false); }

        /// <summary>
        /// This special method is needed because Stream.Write("testString") limits the string length to 128 (7-bit ASCII) although we want to have at least 255
        /// </summary>
        /// <param name="s"></param>
        public void WriteString(string s, bool IsUnicode)
        {
            if (String.IsNullOrEmpty(s))
            {
                if (IsUnicode)
                    BinWriter.Write((int)0);
                else
                    BinWriter.Write((ushort)0);
                return;
            }
            if (IsUnicode)
            {
                byte[] tb = Encoding.Unicode.GetBytes(s);
                BinWriter.Write(tb.Length);
                BinWriter.Write(tb);
            }
            else
            {
                if (s.Length >= ushort.MaxValue - 1)
                    s = s.Remove(ushort.MaxValue - 1);
                BinWriter.Write((ushort)s.Length); // short = 2 bytes; byte = 1 byte
                BinWriter.Write(Encoding.UTF8.GetBytes(s));
            }
        }

        public string ReadString(bool IsUnicode)
        {
            if (IsUnicode)
            {
                int len = BinReader.ReadInt32();
                byte[] t = BinReader.ReadBytes(len);
                return Encoding.Unicode.GetString(t);
            }
            else
            {
                int len = (int)BinReader.ReadUInt16();
                if (len < 1) return String.Empty;
                byte[] t = BinReader.ReadBytes(len);
                return Encoding.UTF8.GetString(t);
            }
        }
        #endregion

        #region Writing
        public void WriteModules(DModule[] Modules) { WriteModules(new List<DModule>(Modules)); }

        public void WriteModules(List<DModule> Modules)
        {
            var bs = BinWriter;

            bs.Write(Modules.Count); // To know how many modules we've saved

            foreach (var mod in Modules)
            {
                bs.Write(ModuleInitializer);
                WriteString(mod.ModuleName, true);
                WriteString(mod.FileName, true);
                WriteNodes(mod.Children);
                bs.Flush();
            }
        }

        public void WriteNode(INode dt)
        {
            var bs = BinWriter;

            bs.Write(NodeInitializer);

            if (dt == null)
            {
                bs.Write((byte)0);
                bs.Flush();
                return;
            }

            // Here it can be a variable or a block statement derivate only
            if (dt is DVariable)
            {
                bs.Write((byte)1);

                byte subtype = 0;
                if (dt is DEnumValue) subtype = 1;
                bs.Write(subtype);

                // Now write type specific properties
                bs.Write((dt as DVariable).IsAlias);
                WriteExpression((dt as DVariable).Initializer);
            }
            else if (dt is IBlockNode)
            {
                bs.Write((byte)2);

                byte subtype = 0;
                if (dt is DMethod) subtype = 1;
                else if (dt is DStatementBlock) subtype = 2;
                else if (dt is DClassLike) subtype = 3;
                else if (dt is DEnum) subtype = 4;
                bs.Write(subtype);

                // Now write type specific properties
                if (dt is DMethod)
                {
                    var n = dt as DMethod;
                    WriteNodes(n.Parameters.ToArray());
                    bs.Write((int)n.SpecialType);
                }
                else if (dt is DStatementBlock)
                {
                    var n = dt as DStatementBlock;
                    bs.Write(n.Token);
                    WriteExpression(n.Expression);
                }
                else if (dt is DClassLike)
                {
                    var n = dt as DClassLike;

                    bs.Write(n.ClassType);
                    bs.Write(n.BaseClasses.Count);
                    foreach (var t in n.BaseClasses)
                        WriteTypeDecl(t);
                }
                // No extra fields for DEnum types

                // Write block statement properties
                var bl = dt as IBlockNode;
                bs.Write(bl.BlockStartLocation.X);
                bs.Write(bl.BlockStartLocation.Y);

                WriteNodes(bl.Children);
            }
            else throw new InvalidCastException("Unknown node type");

            WriteTypeDecl(dt.Type);

            // Write (general) node props
			if (dt is DNode)
			{
				bs.Write((dt as DNode).Attributes.Count);
				foreach (var a in (dt as DNode).Attributes)
				{
					bs.Write(a.Token);
					if (a.LiteralContent == null)
						WriteString(null, true);
					else
						WriteString(a.LiteralContent.ToString(), true);
				}
			}
			else bs.Write((int)0);

            WriteString(dt.Name, true);
			if (dt is DNode)
				WriteNodes((dt as DNode).TemplateParameters);
			else 
				bs.Write((int)0);

            WriteString(dt.Description, true);

            bs.Write(dt.StartLocation.X);
            bs.Write(dt.StartLocation.Y);
            bs.Write(dt.EndLocation.X);
            bs.Write(dt.EndLocation.Y);
        }

        public void WriteNodes(INode[] Nodes)
        {
            var bs = BinWriter;

            if (Nodes == null || Nodes.Length < 1)
            {
                bs.Write((int)0);
                bs.Flush();
                return;
            }

            bs.Write(Nodes.Length);

            foreach (var dt in Nodes)
                WriteNode(dt);

            bs.Flush();
        }

        public void WriteExpression(DExpression e)
        {
            var bs = BinWriter;

            if (e == null)
            {
                bs.Write((byte)0);
                return;
            }

            if (e is IdentExpression)
            {
                bs.Write((byte)1);
                WriteString((e as IdentExpression).Value.ToString(), true);
            }

            else if (e is TokenExpression)
            {
                bs.Write((byte)2);
                bs.Write((e as TokenExpression).Token);
            }

            else if (e is TypeDeclarationExpression)
            {
                bs.Write((byte)3);
                WriteTypeDecl((e as TypeDeclarationExpression).Declaration);
            }

            else if (e is ClampExpression)
            {
                bs.Write((byte)4);

                var c = e as ClampExpression;
                bs.Write((byte)c.Clamps);
                WriteExpression(c.InnerExpression);
            }

            else if (e is AssignTokenExpression)
            {
                bs.Write((byte)5);

                var a = e as AssignTokenExpression;

                bs.Write(a.Token);
                WriteExpression(a.FollowingExpression);
            }

            else if (e is SwitchExpression)
            {
                bs.Write((byte)6);

                var s = e as SwitchExpression;
                WriteExpression(s.FalseCase);
                WriteExpression(s.TrueCase);
            }

            else if (e is ArrayLiteralExpression)
            {
                bs.Write((byte)7);

                var a = e as ArrayLiteralExpression;
                bs.Write((byte)a.Clamps);

                bs.Write(a.Expressions.Count);
                foreach (var ex in a.Expressions)
                    WriteExpression(ex);
            }

            else if (e is FunctionLiteral)
            {
                bs.Write((byte)8);

                var f = e as FunctionLiteral;
                bs.Write(f.LiteralToken);

                WriteNode(f.AnonymousMethod);
            }
            else if (e is InitializerExpression)
            {
                bs.Write((byte)9);

                WriteExpression((e as InitializerExpression).Initializer);
            }

            WriteExpression(e.Base);
        }

        public void WriteTypeDecl(ITypeDeclaration decl)
        {
            var bs = BinWriter;

            if (decl == null) // If there's no type, simply write a 0 to the file
            {
                bs.Write((byte)0);
                return;
            }

            if (decl is DTokenDeclaration)
            {
                bs.Write((byte)1);
                bs.Write((decl as DTokenDeclaration).Token);
            }

            else if (decl is NormalDeclaration)
            {
                bs.Write((byte)2);
                WriteString((decl as NormalDeclaration).Name, true);
            }

            else if (decl is ClampDecl)
            {
                bs.Write((byte)3);

                var cd = decl as ClampDecl;
                bs.Write((byte)cd.Clamps);
                WriteTypeDecl(cd.KeyType);
            }

            else if (decl is PointerDecl)
                bs.Write((byte)4);

            else if (decl is MemberFunctionAttributeDecl)
            {
                var mf = decl as MemberFunctionAttributeDecl;

                bs.Write((byte)5);
                bs.Write(mf.Token);
                WriteTypeDecl(mf.InnerType);
            }

            else if (decl is VarArgDecl)
                bs.Write((byte)6);

            else if (decl is InheritanceDecl)
            {
                bs.Write((byte)7);

                WriteTypeDecl((decl as InheritanceDecl).InheritedClass);
                WriteTypeDecl((decl as InheritanceDecl).InheritedInterface);
            }

            else if (decl is TemplateDecl)
            {
                bs.Write((byte)8);

                bs.Write((decl as TemplateDecl).Template.Count);
                foreach (var td in (decl as TemplateDecl).Template)
                    WriteTypeDecl(td);
            }

            else if (decl is IdentifierList)
            {
                bs.Write((byte)9);

                bs.Write((decl as IdentifierList).Parts.Count);
                foreach (var td in (decl as IdentifierList).Parts)
                    WriteTypeDecl(td);
            }

            else if (decl is DExpressionDecl)
            {
                bs.Write((byte)10);

                WriteExpression((decl as DExpressionDecl).Expression);
            }

            else if (decl is DelegateDeclaration)
            {
                bs.Write((byte)11);

                bs.Write((decl as DelegateDeclaration).IsFunction);
                WriteNodes((decl as DelegateDeclaration).Parameters.ToArray());
            }

            WriteTypeDecl(decl.Base);
        }
        #endregion

        #region Reading
        public List<DModule> ReadModules()
        {
            var bs = BinReader;

            // Module count
            int ModuleCount = bs.ReadInt32();

            var ret = new List<DModule>();

            for (int i = 0; i < ModuleCount; i++)
            {
                if (bs.ReadInt32() != ModuleInitializer)
                    throw new Exception("Wrong data format");

                string mod_name = ReadString(true);
                string mod_fn = ReadString(true);

                var cm = new DModule();
                cm.FileName = mod_fn;
                cm.ModuleName = mod_name;

                var bl = cm as IBlockNode;
                ReadNodes(ref bl);

                ret.Add(cm);
            }

            return ret;
        }

        public void ReadNodes(ref IBlockNode Parent)
        {
            var bs = BinReader;

            int NodeCount = bs.ReadInt32();

            for (int i = 0; i < NodeCount; i++)
            {
                Parent.Add(ReadNode());
            }
        }

        public DNode ReadNode()
        {
            int x = 0, y = 0;

            var s = BinReader;

            if (s.ReadInt32() != NodeInitializer)
                throw new Exception("Node parsing error");

            DNode ret = null;

            int MainType = s.ReadByte();
            int SubType = s.ReadByte();

            if (MainType < 1) return ret;

            // Variable
            if (MainType == 1)
            {
                if (SubType == 1)
                    ret = new DEnumValue();
                else
                    ret = new DVariable();

                (ret as DVariable).IsAlias = s.ReadBoolean();
                (ret as DVariable).Initializer = ReadExpression();
            }
            // Block
            else if (MainType == 2)
            {
                if (SubType < 1)
                    throw new Exception("Block subtype must not be 0");
                // DMethod
                else if (SubType == 1)
                {
                    ret = new DMethod();

                    // Synthetic node
                    var tbl = new DStatementBlock() as IBlockNode;
                    ReadNodes(ref tbl);

                    foreach (var p in tbl)
                    {
                        (p as DNode).Parent = ret as IBlockNode;
                        (ret as DMethod).Parameters.Add(p);
                    }
                    (ret as DMethod).SpecialType = (DMethod.MethodType)s.ReadInt32();
                }
                // DStatementBlock
                else if (SubType == 2)
                {
                    ret = new DStatementBlock();

                    (ret as DStatementBlock).Token = s.ReadInt32();
                    (ret as DStatementBlock).Expression = ReadExpression();
                }
                // DClassLike
                else if (SubType == 3)
                {
                    ret = new DClassLike();

                    (ret as DClassLike).ClassType = s.ReadInt32();

                    int BaseClassCount = s.ReadInt32();
                    for (int j = 0; j < BaseClassCount; j++)
                        (ret as DClassLike).BaseClasses.Add(ReadTypeDeclaration());
                }

                else if (SubType == 4)
                {
                    ret = new DEnum();
                }


                var bl = ret as IBlockNode;
                x = s.ReadInt32();
                y = s.ReadInt32();
                bl.BlockStartLocation = new CodeLocation(x, y);

                ReadNodes(ref bl);
            }

            ret.Type = ReadTypeDeclaration();

            int AttributeCount = s.ReadInt32();
            for (int j = 0; j < AttributeCount; j++)
            {
                var attr = new DAttribute(s.ReadInt32());
                var o = ReadString(true);
                if (!String.IsNullOrEmpty(o))
                    attr.LiteralContent = o;
                ret.Attributes.Add(attr);
            }

            ret.Name = ReadString(true);
            // Template parameters
            var tp = new DStatementBlock() as IBlockNode;
            ReadNodes(ref tp);

            if ((tp as IBlockNode).Count > 0)
                ret.TemplateParameters = (tp as IBlockNode).Children;
            ret.Description = ReadString(true);

            x = s.ReadInt32();
            y = s.ReadInt32();
            ret.StartLocation = new CodeLocation(x, y);
            x = s.ReadInt32();
            y = s.ReadInt32();
            ret.EndLocation = new CodeLocation(x, y);

            return ret;
        }

        public ITypeDeclaration ReadTypeDeclaration()
        {
            var s = BinReader;

            ITypeDeclaration ret = null;

            byte type = s.ReadByte();

            if (type == 0)
                return null;
            if (type == 1)
                ret = new DTokenDeclaration(s.ReadInt32());
            else if (type == 2)
                ret = new NormalDeclaration(ReadString(true));
            else if (type == 3)
            {
                ret = new ClampDecl();
                (ret as ClampDecl).Clamps = (ClampDecl.ClampType)s.ReadByte();
                (ret as ClampDecl).KeyType = ReadTypeDeclaration();
            }
            else if (type == 4)
                ret = new PointerDecl();
            else if (type == 5)
            {
                ret = new MemberFunctionAttributeDecl(s.ReadInt32());
                (ret as MemberFunctionAttributeDecl).InnerType = ReadTypeDeclaration();
            }
            else if (type == 6)
                ret = new VarArgDecl();
            else if (type == 7)
            {
                ret = new InheritanceDecl();

                (ret as InheritanceDecl).InheritedClass = ReadTypeDeclaration();
                (ret as InheritanceDecl).InheritedInterface = ReadTypeDeclaration();
            }
            else if (type == 8)
            {
                ret = new TemplateDecl();

                int cnt = s.ReadInt32();
                for (int j = 0; j < cnt; j++)
                    (ret as TemplateDecl).Template.Add(ReadTypeDeclaration());
            }
            else if (type == 9)
            {
                ret = new IdentifierList();

                int cnt = s.ReadInt32();
                for (int j = 0; j < cnt; j++)
                    (ret as IdentifierList).Parts.Add(ReadTypeDeclaration());
            }
            else if (type == 10)
                ret = new DExpressionDecl(ReadExpression());
            else if (type == 11)
            {
                ret = new DelegateDeclaration();

                (ret as DelegateDeclaration).IsFunction = s.ReadBoolean();

                // Parameters
                var bl = new DStatementBlock() as IBlockNode;
                ReadNodes(ref bl);
                (ret as DelegateDeclaration).Parameters.AddRange(bl.Children);
            }
            else throw new Exception("Unknown type type");

            ret.Base = ReadTypeDeclaration();

            return ret;
        }

        public DExpression ReadExpression()
        {
            var s = BinReader;
            DExpression e = null;

            int type = s.ReadByte();
            if (type < 1) return null;

            if (type == 1)
                e = new IdentExpression(ReadString(true));
            else if (type == 2)
                e = new TokenExpression(s.ReadInt32());
            else if (type == 3)
                e = new TypeDeclarationExpression(ReadTypeDeclaration());
            else if (type == 4)
            {
                e = new ClampExpression((ClampExpression.ClampType)s.ReadByte());
                (e as ClampExpression).InnerExpression = ReadExpression();
            }
            else if (type == 5)
            {
                e = new AssignTokenExpression(s.ReadInt32());
                (e as AssignTokenExpression).FollowingExpression = ReadExpression();
            }
            else if (type == 6)
            {
                e = new SwitchExpression();
                (e as SwitchExpression).FalseCase = ReadExpression();
                (e as SwitchExpression).TrueCase = ReadExpression();
            }
            else if (type == 7)
            {
                e = new ArrayLiteralExpression((ClampExpression.ClampType)s.ReadByte());

                int cnt = s.ReadInt32();
                for (int j = 0; j < cnt; j++)
                    (e as ArrayLiteralExpression).Expressions.Add(ReadExpression());
            }
            else if (type == 8)
            {
                e = new FunctionLiteral(s.ReadInt32());

                (e as FunctionLiteral).AnonymousMethod = ReadNode() as DMethod;
            }
            else if (type == 9)
            {
                e = new InitializerExpression(ReadExpression());
            }

            e.Base = ReadExpression();
            return e;
        }
        #endregion
    }
	*/
}
