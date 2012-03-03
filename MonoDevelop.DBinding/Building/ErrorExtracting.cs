using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.CodeDom.Compiler;
using MonoDevelop.Core;
using MonoDevelop.Projects;

namespace MonoDevelop.D.Building
{
    /// <summary>
    /// Contains methods which care about finding and extracting compiler errors from output strings
    /// </summary>
    class ErrorExtracting
    {
        static Regex dmdCompileRegex = new Regex(@"\s*(?<file>.*)\((?<line>\d*)\):\s*(?<type>Error|Warning|Note):(\s*)(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex withColRegex = new Regex(
            @"^\s*(?<file>.*):(?<line>\d*):(?<column>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static Regex noColRegex = new Regex(
            @"^\s*(?<file>.*):(?<line>\d*):\s*(?<level>.*)\s*:\s(?<message>.*)",
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

        public static BuildError FindError(string errorString, TextReader reader)
        {
            var error = new BuildError();
            string warning = GettextCatalog.GetString("warning");
            string note = GettextCatalog.GetString("note");

            var match = dmdCompileRegex.Match(errorString);
            int line = 0;

            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                int.TryParse(match.Groups["line"].Value, out line);
                error.Line = line;
                error.IsWarning = match.Groups["type"].Value == "Warning" || match.Groups["type"].Value == "Note";
                error.ErrorText = match.Groups["message"].Value;

                return error;
            }


            match = withColRegex.Match(errorString);

            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                error.Line = int.Parse(match.Groups["line"].Value);
                error.Column = int.Parse(match.Groups["column"].Value);
                error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
                                   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
                error.ErrorText = match.Groups["message"].Value;

                return error;
            }

            match = noColRegex.Match(errorString);

            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                error.Line = int.Parse(match.Groups["line"].Value);
                error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
                                   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
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
                error.Line = int.Parse(match.Groups["line"].Value);

                error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
                                   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
                error.ErrorText = match.Groups["message"].Value;

                return error;
            }

            match = gcclinkerRegex.Match(errorString);
            if (match.Success)
            {
                error.FileName = match.Groups["file"].Value;
                error.Line = int.Parse(match.Groups["line"].Value);

                error.IsWarning = (match.Groups["level"].Value.Equals(warning, StringComparison.Ordinal) ||
                                   match.Groups["level"].Value.Equals(note, StringComparison.Ordinal));
                error.ErrorText = match.Groups["message"].Value;


                return error;
            }


            return null;
        }
    }
}
