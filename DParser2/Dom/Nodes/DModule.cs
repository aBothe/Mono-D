using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Dom.Statements;
using D_Parser.Parser;
using System;
using System.IO;
using System.Collections.ObjectModel;

namespace D_Parser.Dom
{
    /// <summary>
    /// Encapsules an entire document and represents the root node
    /// </summary>
    public class DModule : DBlockNode, IAbstractSyntaxTree
    {
		public readonly DateTime ParseTimestamp = DateTime.Now;

        /// <summary>
        /// Applies file name, children and imports from an other module instance
         /// </summary>
        /// <param name="Other"></param>
        public override void AssignFrom(INode Other)
        {
			if (Other is IAbstractSyntaxTree)
				ParseErrors = ((IAbstractSyntaxTree)Other).ParseErrors;

			base.AssignFrom(Other);
        }

		string _FileName;

		/// <summary>
		/// Name alias
		/// </summary>
		public string ModuleName
		{
			get { return Name; }
			set { Name = value; }
		}

		public string FileName
		{
			get
			{
				return _FileName;
			}
			set
			{
				_FileName = value;
			}
		}

		/// <summary>
		/// Returns a package-module name-combination (like std.stdio) in dependency of its base directory (e.g. C:\dmd2\src\phobos)
		/// </summary>
		public static string GetModuleName(string baseDirectory, IAbstractSyntaxTree ast)
		{
			return GetModuleName(baseDirectory, ast.FileName);
		}

		/// <summary>
		/// Returns the relative module name including its packages based on the baseDirectory parameter.
		/// If the file isn't located in the base directory, the file name minus the extension is returned only.
		/// </summary>
		public static string GetModuleName(string baseDirectory, string file)
		{
			if (file!=null && baseDirectory != null && file.StartsWith(baseDirectory))
				return Path.ChangeExtension(
						file.Substring(baseDirectory.Length), null).
							Replace(Path.DirectorySeparatorChar, '.').Trim('.');
			else
				return Path.GetFileNameWithoutExtension(file);
		}

		public System.Collections.ObjectModel.ReadOnlyCollection<ParserError> ParseErrors
		{
			get;
			set;
		}

		/// <summary>
		/// A module's first statement can be a module ABC; statement. If so, this variable will keep it.
		/// </summary>
		public ModuleStatement OptionalModuleStatement;

		public override string ToString(bool Attributes, bool IncludePath)
		{
			if (!IncludePath)
			{
				var parts = ModuleName.Split('.');
				return parts[parts.Length-1];
			}

			return ModuleName;
		}

		public override void Accept(NodeVisitor vis)
		{
			vis.Visit(this);
		}

		public override R Accept<R>(NodeVisitor<R> vis)
		{
			return vis.Visit(this);
		}
	}
}
