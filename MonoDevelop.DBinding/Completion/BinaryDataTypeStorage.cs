using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using D_Parser;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using D_Parser.Core;

namespace D_IDE.D.CodeCompletion
{/*
	class BinaryDataTypeStorageWriter
	{
		#region General
		public BinaryWriter BinStream;
		public const uint ModuleInitializer = (uint)('D') | ('M' << 8) | ('o' << 16) | ('d' << 24);
		public const uint NodeInitializer = (uint)('N') | ('o' << 8) | ('d' << 16) | ('e' << 24);

		public BinaryDataTypeStorageWriter()
		{
			BinStream = new BinaryWriter(new MemoryStream());
		}

		public BinaryDataTypeStorageWriter(string file)
		{
			FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write);
			BinStream = new BinaryWriter(fs);
		}

		public void Close()
		{
			if (BinStream != null)
			{
				BinStream.Flush();
				BinStream.Close();
			}
		}

		void WriteString(string s) { WriteString(s, false); }

		/// <summary>
		/// This special method is needed because Stream.Write("testString") limits the string length to 128 (7-bit ASCII) although we want to have at least 255
		/// </summary>
		/// <param name="s"></param>
		void WriteString(string s, bool IsUnicode)
		{
			if (String.IsNullOrEmpty(s))
			{
				if (IsUnicode)
					BinStream.Write((int)0);
				else
					BinStream.Write((ushort)0);
				return;
			}
			if (IsUnicode)
			{
				byte[] tb = Encoding.Unicode.GetBytes(s);
				BinStream.Write(tb.Length);
				BinStream.Write(tb);
			}
			else
			{
				if (s.Length >= ushort.MaxValue - 1)
					s = s.Remove(ushort.MaxValue - 1);
				BinStream.Write((ushort)s.Length); // short = 2 bytes; byte = 1 byte
				BinStream.Write(Encoding.UTF8.GetBytes(s));
			}
		}
		#endregion

		#region Modules
		public void WriteASTStorage(ASTStorage Storage)
		{
			var bs=BinStream;

			bs.Write(Storage.ParsedGlobalDictionaries.Count);

			foreach(var pd in Storage)
			{
				WriteString(pd.BaseDictionary);

				bs.Write(pd.Count);

				if(pd.Count<1)
					break;

				foreach(var mod in pd)
					if(mod is DModule)
						WriteModule(mod as DModule);
			}
		}

		void WriteModule(DModule Module)
		{
			var bs=BinStream;
			bs.Write(ModuleInitializer);
				WriteString(Module.ModuleName,true);
				WriteString(Module.FileName, true);
				WriteNodes(Module.Children);
			bs.Flush();
		}
		#endregion

		#region Nodes
		void WriteNodes(INode[] Nodes)
		{
			BinaryWriter bs = BinStream;

			if (Nodes == null || Nodes.Length < 1)
			{
				bs.Write((int)0);
				bs.Flush();
				return;
			}

			bs.Write(Nodes.Length);

			foreach (INode _dt in Nodes)
			{
				var dt=_dt as DNode;
				bs.Write(NodeInitializer);

				bs.Write((int));
				WriteString(dt.name);
				bs.Write((int)dt.TypeToken);
				WriteTypeDecl(dt.Type);
				WriteString(dt.desc, true);
				bs.Write(dt.StartLocation.X);
				bs.Write(dt.StartLocation.Y);
				bs.Write(dt.EndLocation.X);
				bs.Write(dt.EndLocation.Y);

				bs.Write(dt.modifiers.Count);
				foreach (int mod in dt.modifiers)
					bs.Write(mod);

				WriteString(dt.module);
				if (dt is DVariable)
					WriteString((dt as DVariable).Value, true);

				if (dt is DClassLike)
				{
					bs.Write((byte)(dt as DClassLike).BaseClasses.Count);
					foreach (TypeDeclaration td in (dt as DClassLike).BaseClasses)
						WriteTypeDecl(td);
				}

				if (dt is DEnum)
					WriteTypeDecl((dt as DEnum).EnumBaseType);

				WriteNodes(dt.TemplateParameters);
				if (dt is DMethod) WriteNodes((dt as DMethod).Parameters);
				WriteNodes(dt.Children);
			}

			bs.Flush();
		}

		void WriteTypeDecl(TypeDeclaration decl)
		{
			BinaryWriter bs = BinStream;

			if (decl == null) // If there's no type, simply write a 0 to the file
			{
				bs.Write((byte)0);
				return;
			}
			bs.Write(decl.TypeId);

			if (decl is DTokenDeclaration)
				bs.Write((decl as DTokenDeclaration).Token);
			else if (decl is NormalDeclaration)
				WriteString((decl as NormalDeclaration).Name, true);
			else if (decl is ArrayDecl)
				WriteTypeDecl((decl as ArrayDecl).KeyType);
			else if (decl is DelegateDeclaration)
			{
				bs.Write((decl as DelegateDeclaration).IsFunction);
				WriteNodes((decl as DelegateDeclaration).Parameters);
			}
			else if (decl is InheritanceDecl)
			{
				WriteTypeDecl((decl as InheritanceDecl).InheritedClass);
				WriteTypeDecl((decl as InheritanceDecl).InheritedInterface);
			}
			else if (decl is TemplateDecl)
				WriteTypeDecl((decl as TemplateDecl).Template);
			else if (decl is DotCombinedDeclaration)
				WriteTypeDecl((decl as DotCombinedDeclaration).AccessedMember);

			WriteTypeDecl(decl.Base);
		}
		#endregion
	}
	*/

	/*
	class BinaryDataTypeStorageReader
	{
		#region General
		public BinaryReader BinStream;
		public BinaryDataTypeStorageReader(string file)
		{
			FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
			BinStream = new BinaryReader(fs);
		}

		string ReadString() { return ReadString(false); }

		string ReadString(bool IsUnicode)
		{
			if (IsUnicode)
			{
				int len = BinStream.ReadInt32();
				byte[] t = BinStream.ReadBytes(len);
				return Encoding.Unicode.GetString(t);
			}
			else
			{
				int len = (int)BinStream.ReadUInt16();
				if (len < 1) return String.Empty;
				byte[] t = BinStream.ReadBytes(len);
				return Encoding.UTF8.GetString(t);
			}
		}

		public void Close()
		{
			if (BinStream != null)
			{
				BinStream.Close();
			}
		}
		#endregion

		#region Modules
		public List<DModule> ReadModules(ref List<string> ParsedDirectories)
		{
			BinaryReader bs = BinStream;

			int Count = bs.ReadInt32();
			List<DModule> ret = new List<DModule>(Count); // Speed improvement caused by given number of modules

			uint DirCount = bs.ReadUInt32();
			for (int i = 0; i < DirCount; i++)
			{
				string dir = ReadString(true);
				if (!ParsedDirectories.Contains(dir)) ParsedDirectories.Add(dir);
			}

			for (int i = 0; i < Count; i++)
			{
				uint mi = bs.ReadUInt32();
				if (mi != BinaryDataTypeStorageWriter.ModuleInitializer)
				{
					throw new Exception("Wrong module definition format!");
				}

				DModule mod = new DModule();
				mod.ModuleName = ReadString();
				mod.mod_file = ReadString(true);
				mod.dom.name = mod.ModuleName;
				mod.dom.module = mod.ModuleName;
				ReadNodes(ref mod.dom.children);
				ret.Add(mod);
			}
			return ret;
		}
		#endregion

		#region Nodes
		void ReadNodes(ref List<DNode> Nodes)
		{
			BinaryReader bs = BinStream;

			int Count = bs.ReadInt32();
			Nodes.Capacity = Count;
			Nodes.Clear();

			for (int i = 0; i < Count; i++)
			{
				uint ni = bs.ReadUInt32();
				if (ni != BinaryDataTypeStorageWriter.NodeInitializer)
				{
					throw new Exception("Wrong node definition format!");
				}

				DNode dt = new DNode();

				dt.fieldtype = (FieldType)bs.ReadInt32();
				switch (dt.fieldtype)
				{
					default: break;
					case FieldType.Class:
					case FieldType.Interface:
					case FieldType.Struct:
					case FieldType.Template:
						dt = new DClassLike();
						break;
					case FieldType.Enum:
						dt = new DEnum();
						break;
					case FieldType.EnumValue:
						dt = new DEnumValue();
						break;
					case FieldType.Delegate:
					case FieldType.Constructor: // Also a ctor is treated as a method here
					case FieldType.Function:
						dt = new DMethod();
						break;
					case FieldType.AliasDecl:
					case FieldType.Variable:
						dt = new DVariable();
						break;
				}
				dt.name = ReadString();
				dt.TypeToken = bs.ReadInt32();
				dt.Type = ReadTypeDecl();
				dt.desc = ReadString(true);
				D_Parser.Location startLoc = new D_Parser.Location();
				startLoc.X = bs.ReadInt32();
				startLoc.Y = bs.ReadInt32();
				dt.StartLocation = startLoc;
				D_Parser.Location endLoc = new D_Parser.Location();
				endLoc.X = bs.ReadInt32();
				endLoc.Y = bs.ReadInt32();
				dt.EndLocation = endLoc;

				int modCount = bs.ReadInt32();
				for (int j = 0; j < modCount; j++)
					dt.modifiers.Add(bs.ReadInt32());

				dt.module = ReadString();
				if (dt is DVariable)
					(dt as DVariable).Value = ReadString(true);

				if (dt is DClassLike)
				{
					byte InhCount = bs.ReadByte();
					for (int j = 0; j < InhCount; j++)
						(dt as DClassLike).BaseClasses.Add(ReadTypeDecl());
				}

				if (dt is DEnum)
					(dt as DEnum).EnumBaseType = ReadTypeDecl();

				ReadNodes(ref dt.TemplateParameters);
				if (dt is DMethod) ReadNodes(ref (dt as DMethod).Parameters);
				ReadNodes(ref dt.children);

				Nodes.Add(dt);
			}
		}

		TypeDeclaration ReadTypeDecl()
		{
			TypeDeclaration ret = null;
			BinaryReader bs = BinStream;

			byte declType = bs.ReadByte();
			if (declType < 1) return null;

			if (declType == DTokenDeclaration.GetDeclarationClassTypeId)
				ret = new DTokenDeclaration(bs.ReadInt32());
			else if (declType == NormalDeclaration.GetDeclarationClassTypeId)
				ret = new NormalDeclaration(ReadString(true));
			else if (declType == ArrayDecl.GetDeclarationClassTypeId)
			{
				ret = new ArrayDecl();
				(ret as ArrayDecl).KeyType = ReadTypeDecl();
			}
			else if (declType == DelegateDeclaration.GetDeclarationClassTypeId)
			{
				DelegateDeclaration dd = new DelegateDeclaration();
				ret = dd;
				dd.IsFunction = bs.ReadBoolean();
				ReadNodes(ref dd.Parameters);
			}
			else if (declType == PointerDecl.GetDeclarationClassTypeId)
				ret = new PointerDecl();
			else if (declType == MemberFunctionAttributeDecl.GetDeclarationClassTypeId)
				ret = new MemberFunctionAttributeDecl(bs.ReadInt32());
			else if (declType == VarArgDecl.GetDeclarationClassTypeId)
				ret = new VarArgDecl();
			else if (declType == InheritanceDecl.GetDeclarationClassTypeId)
			{
				InheritanceDecl id = new InheritanceDecl();
				id.InheritedClass = ReadTypeDecl();
				id.InheritedInterface = ReadTypeDecl();
				ret = id;
			}
			else if (declType == TemplateDecl.GetDeclarationClassTypeId)
			{
				ret = new TemplateDecl();
				(ret as TemplateDecl).Template = ReadTypeDecl();
			}
			else if (declType == DotCombinedDeclaration.GetDeclarationClassTypeId)
			{
				ret = new DotCombinedDeclaration();
				(ret as DotCombinedDeclaration).AccessedMember = ReadTypeDecl();
			}
			else return null;

			ret.Base = ReadTypeDecl();
			return ret;
		}
		#endregion
	}
*/}
