using System;
using System.Text.RegularExpressions;
using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects;
using D_Parser.Resolver;
using D_Parser.Dom;

namespace MonoDevelop.D.Building
{
    /// <summary>
    /// Contains methods which care about finding and extracting compiler errors from output strings
    /// </summary>
    static class ErrorExtracting
    {
		static Regex dmdCompileRegex = new Regex(@"\s*(?<file>.*)\((?<line>\d*)(,(?<col>\d+?))?\):\s*(?<type>Error|Warning|Note):(\s*)(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex withColRegex = new Regex(
			@"^\s*(?<file>.*):(?<line>\d*)(:(?<column>\d+?))?:\s*(?<level>.*)\s*:\s(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static Regex linkerRegex = new Regex(
            @"^\s*(?<file>[^:]*):(?<line>\d*):\s*(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        //additional regex parsers
        private static Regex noColRegex_2 = new Regex(
            @"^\s*((?<file>.*)(\()(?<line>\d*)(\)):\s*(?<message>.*))|(Error:)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex gcclinkerRegex = new Regex(
            @"^\s*(?<file>.*):(?<line>\d*):((?<column>\d*):)?\s*(?<level>.*)\s*:\s(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		static readonly Regex mixinInlineRegex = new Regex("-mixin-(?<line>\\d+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		/// <summary>
		/// Checks a compilation return code, 
		/// and adds an error result if the compiler results
		/// show no errors.
		/// </summary>
		/// <param name="monitor"></param>
		/// <param name="br"> A <see cref="BuildResult"/>: The return code from a build run.</param>
		/// <param name="returnCode">A <see cref="System.Int32"/>: A process return code.</param>
		public static bool HandleReturnCode(IProgressMonitor monitor, BuildResult br, int returnCode)
		{
			if (returnCode != 0)
			{
				if (monitor != null)
					monitor.Log.WriteLine("Exit code " + returnCode.ToString());

				if(br.ErrorCount == 0)
					br.AddError(string.Empty, 0, 0, string.Empty,
						GettextCatalog.GetString("Build failed - check build output for details"));

				return false;
			}
			return true;
		}

		const int MaxErrorMsgLength = 500;
		/// <summary>
		/// Scans errorString line-wise for filename-line-message patterns (e.g. "myModule(1): Something's wrong here") and add these error locations to the CompilerResults cr.
		/// </summary>
		public static void HandleCompilerOutput(AbstractDProject Project, BuildResult br, string errorString)
		{
			var reader = new StringReader(errorString);
			string next;

			while ((next = reader.ReadLine()) != null)
			{
				var error = ErrorExtracting.FindError(next, reader);
				if (error != null)
				{
					if (error.ErrorText != null && error.ErrorText.Length > MaxErrorMsgLength)
						error.ErrorText = error.ErrorText.Substring (0, MaxErrorMsgLength) + "...";

					// dmd's error filenames may contain mixin location info
					var m = mixinInlineRegex.Match (error.FileName);
					if (m.Success) {
						error.FileName = error.FileName.Substring (0, m.Index);
						int line;
						int.TryParse (m.Groups ["line"].Value, out line);
						error.Line = line;
					}

					if (!Path.IsPathRooted(error.FileName))
						error.FileName = Project.GetAbsoluteChildPath(error.FileName);
					br.Append(error);
				}
			}

			reader.Close();
		}

		public static bool IsWarning(string type)
		{
			type = type.ToLowerInvariant ();
			return type != "error"; 
		}

        public static BuildError FindError(string errorString, TextReader reader)
        {
            var error = new BuildError();

            var match = dmdCompileRegex.Match(errorString);
            int line = 0;

            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                int.TryParse(match.Groups["line"].Value, out line);
                error.Line = line;
				if(int.TryParse(match.Groups["col"].Value, out line))
					error.Column = line;
				error.IsWarning = IsWarning(match.Groups ["type"].Value);
                error.ErrorText = match.Groups["message"].Value;

                return error;
            }


            match = withColRegex.Match(errorString);

            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                error.Line = int.Parse(match.Groups["line"].Value);
				if(int.TryParse(match.Groups["column"].Value, out line))
					error.Column = line;
				error.IsWarning = IsWarning(match.Groups ["level"].Value);
                error.ErrorText = match.Groups["message"].Value;

				// Skip messages that begin with ( and end with ), since they're generic.
				//Attempt to capture multi-line versions too.
				if (error.ErrorText.StartsWith("("))
				{
					string error_continued = error.ErrorText;
					do
					{
						if (error_continued.EndsWith(")"))
							return null;
					} while ((error_continued = reader.ReadLine()) != null);
				}

                return error;
            }

            match = noColRegex_2.Match(errorString);
            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
				int i;
				int.TryParse(match.Groups["line"].Value, out i);
				error.Line = i;
				error.IsWarning = IsWarning(match.Groups ["level"].Value);
                error.ErrorText = match.Groups["message"].Value;

				if(error.FileName.Length > 0 || error.ErrorText.Length > 0)
					return error;
            }

            match = gcclinkerRegex.Match(errorString);
            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                error.Line = int.Parse(match.Groups["line"].Value);
				error.IsWarning = IsWarning(match.Groups ["level"].Value);
                error.ErrorText = match.Groups["message"].Value;
                return error;
            }
            return null;
        }

		/// <summary>
		/// Default OptLink regex for recognizing errors and their origins
		/// </summary>
		static Regex optlinkRegex = new Regex(
			@"\n(?<obj>[a-zA-Z0-9/\\.]+)\((?<module>[a-zA-Z0-9]+)\) (?<offset>[a-zA-Z0-9 ]+)?(\r)?\n Error (?<code>\d*): (?<message>[a-zA-Z0-9_ :]+)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		static Regex symbolUndefRegex = new Regex(
			@"Symbol Undefined (?<mangle>[a-zA-Z0-9_]+)",
			RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		public static void HandleOptLinkOutput(AbstractDProject Project,BuildResult br, string linkerOutput)
		{
			var matches = optlinkRegex.Matches(linkerOutput);

			var ctxt = ResolutionContext.Create(Project == null ? DCompilerService.Instance.GetDefaultCompiler().GenParseCacheView() :
												Project.ParseCache, null, null);

			ctxt.ContextIndependentOptions =
				ResolutionOptions.IgnoreAllProtectionAttributes |
				ResolutionOptions.DontResolveBaseTypes |
				ResolutionOptions.DontResolveBaseClasses |
				ResolutionOptions.DontResolveAliases;

			foreach (Match match in matches)
			{
				var error = new BuildError();

				// Get associated D source file
				if (match.Groups["obj"].Success)
				{
					var obj = Project.GetAbsoluteChildPath(new FilePath(match.Groups["obj"].Value)).ChangeExtension(".d");

					foreach (var pf in Project.Files)
						if (pf.FilePath == obj)
						{
							error.FileName = pf.FilePath;
							break;
						}
				}

				var msg = match.Groups["message"].Value;

				var symUndefMatch = symbolUndefRegex.Match(msg);

				if (symUndefMatch.Success && symUndefMatch.Groups["mangle"].Success)
				{
					var mangledSymbol = symUndefMatch.Groups["mangle"].Value;
					ITypeDeclaration qualifier;
					try
					{
						var resSym = D_Parser.Misc.Mangling.Demangler.DemangleAndResolve(mangledSymbol, ctxt, out qualifier);
						if (resSym is DSymbol)
						{
							var ds = resSym as DSymbol;
							var ast = ds.Definition.NodeRoot as DModule;
							if (ast != null)
								error.FileName = ast.FileName;
							error.Line = ds.Definition.Location.Line;
							error.Column = ds.Definition.Location.Column;
							msg = ds.Definition.ToString(false, true);
						}
						else
							msg = qualifier.ToString();
					}
					catch (Exception ex)
					{
						msg = "<log analysis error> " + ex.Message;
					}
					error.ErrorText = msg + " could not be resolved - library reference missing?";
				}
				else
					error.ErrorText = "Linker error " + match.Groups["code"].Value + " - " + msg;

				br.Append(error);
			}
		}
    }
}
