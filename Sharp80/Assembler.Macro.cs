﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sharp80.Assembler
{
    internal partial class Assembler
    {
        private class Macro
        {
            public string Name { get; private set; }

            private List<string> arguments;
            private List<string> lines = new List<string>();

            public Macro(string line)
            {
                Name = GetCol(line, 0).Replace(":", String.Empty);

                Debug.Assert(GetCol(line, 1) == "MACRO");

                arguments = new List<string>(GetCSV(GetCol(line, 2), 10000));

                for (int i = 0; i < arguments.Count; i++)
                {
                    Debug.Assert(arguments[i] == arguments[i].ToUpper());
                }
            }
            public void AddLine(string Line)
            {
                this.lines.Add(Line);
            }
            public List<string> Expand(string inputArguments, int InputLineNumber, out string Error)
            {
                Error = String.Empty;

                string line;
                List<string> returnLines = new List<string>();
                int argNum;
                string[] inputArgs = GetCSV(inputArguments, 1000);

                if (inputArgs.Length != arguments.Count)
                    Error = string.Format("Macro {0} Arguments Mismatch: {1} Required, {2} Specified, Line {3}",
                                           Name, 
                                           arguments.Count, 
                                           inputArgs.Length, 
                                           InputLineNumber);

                foreach (string l in lines)
                {
                    argNum = 0;
                    line = l;
                    foreach (string a in arguments)
                    {
                        if (argNum < inputArgs.Length)
                            line = line.Replace("&" + a, inputArgs[argNum++]);
                        else
                            line = line.Replace("&" + a, String.Empty);
                    }
                    returnLines.Add(line);
                }
                return returnLines;
            }
        }
    }
}
