using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grep
{
    public class GrepResult
    {
        private string path;
        private string fullPath;
        private int lineNumber;
        private string text;

        public GrepResult(string basePath, string result)
        {
            if (result.StartsWith(basePath))
                result = result.Substring(basePath.Length);

            string[] resultParts = result.Split(new char[] { ':' }, 3);

            if (resultParts.Length == 3 && int.TryParse(resultParts[1], out lineNumber))
            {
                fullPath = basePath + resultParts[0];
                path = resultParts[0];
                lineNumber = int.Parse(resultParts[1]);
                text = resultParts[2].Trim();
            }
            else
            {
                text = result;
                lineNumber = 0;
                path = string.Empty;
                fullPath = string.Empty;
            }
        }

        public string Path
        { get { return this.path; } }

        public string FullPath
        { get { return this.fullPath; } }

        public int LineNumber
        { get { return this.lineNumber; } }

        public string Text
        { get { return this.text; } }

        public string ToCsv()
        {
            return string.Format("\"{0}\",\"{1}\",\"{2}\"",
                path,
                lineNumber,
                text.Replace("\"", "\"\""));
        }

        public string ToTsv()
        {
            return string.Format("\"{0}\"\t\"{1}\"\t\"{2}\"",
                path,
                lineNumber,
                text.Replace("\"", "\"\""));
        }
    }
}
