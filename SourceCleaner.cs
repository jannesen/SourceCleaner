﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Jannesen.Tools.SourceCleaner
{
    class SourceCleaner
    {
        public static   Regex           _beginblock       = new Regex(@"^\s+{\s*$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        public static   Regex           _allowappendblock = new Regex(@"^\s+(if(\s|\().*\)"         +
                                                                          @"|else"                  +
                                                                          @"|for(\s|\().*\)"        +
                                                                          @"|foreach(\s|\().*\)"    +
                                                                          @"|while(\s|\().*\)"      +
                                                                          @"|do"                    +
                                                                          @"|try"                   +
                                                                          @"|catch(\s|\().*\)"      +
                                                                          @"|finally"               +
                                                                          @"|get"                   +
                                                                          @"|set"                   +
                                                                          @")$", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public          int             InputTabSize;
        public          int             OutputTabSize;
        public          bool            TrimTralingSpace;
        public          string          EOL;
        public          bool            BlockReformat;

        public          byte[]          SrcData;
        public          Encoding        Encoding;
        public          List<string>    Lines;

        public                          SourceCleaner() {
            InputTabSize     = 4;
            OutputTabSize    = 0;
            TrimTralingSpace = true;
            EOL              = "\r\n";
            BlockReformat    = false;
        }

        public          void            Run(Globbing globber)
        {
            foreach(var filename in globber.Files)
                Run(filename);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public          void            Run(string filename)
        {
            try {
                SrcData  = null;
                Encoding = null;
                Lines    = null;

                _readFile(filename);

                if (TrimTralingSpace || InputTabSize > 0 || OutputTabSize > 0) {
                    _tabOptimalisation();
                }

                _removeTrailingEmptyLines();

                if (BlockReformat) {
                    _beginBlockReformat();
                }

                _writeFile(filename);
            }
            catch(Exception err) {
                throw new Exception("Processing '" + filename + "' failed.", err);
            }
        }

        private         void            _tabOptimalisation()
        {
            for (int l = 0 ; l < Lines.Count ; ++l) {
                var line = Lines[l];

                if (TrimTralingSpace)
                    line = _removeTralingSpace(line);

                if (InputTabSize > 0)
                    line = _replaceTabsToSpaces(line);

                if (OutputTabSize > 0)
                    line = _replaceSpaceToTabLeading(line);

                Lines[l] = line;
            }
        }
        private         void            _removeTrailingEmptyLines()
        {
            while (Lines.Count > 0 && Lines[Lines.Count-1].Length == 0)
                Lines.RemoveAt(Lines.Count-1);
        }
        private         void            _beginBlockReformat()
        {
            for (int l = 1 ; l < Lines.Count ; ++l) {
                if (_beginblock.IsMatch(Lines[l]) && _allowappendblock.IsMatch(Lines[l - 1])) {
                    Lines[l - 1] = Lines[l - 1] + " {";
                    Lines.RemoveAt(l);
                    --l;
                }
            }
        }
        private         string          _removeTralingSpace(string line)
        {
            int i = line.Length;

            while (i > 0 && (line[i-1] == ' ' || line[i-1] == '\t'))
                --i;

            if (i != line.Length)
                line = line.Substring(0, i);

            return line;
        }
        private         string          _replaceTabsToSpaces(string line)
        {
            int     i;

            while ((i = line.IndexOf('\t')) >= 0)
                line = line.Substring(0, i) + (new string(' ', InputTabSize - (i % InputTabSize))) + line.Substring(i + 1);

            return line;
        }
        private         string          _replaceSpaceToTabLeading(string line)
        {
            int i = 0;
            int p = 0;

            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) {
                p = (line[i] == '\t') ? (p - p % InputTabSize) + InputTabSize : p+1;
                ++i;
            }

            if (i>0) {
                int tabs   = p / OutputTabSize;
                int spaces = p % OutputTabSize;

                if (tabs+spaces != i)
                    line = (new string('\t', tabs)) + (new string(' ', spaces)) + line.Substring(i);
            }

            return line;
        }

        private         void            _readFile(string filename)
        {
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int length = (int)file.Length;
                SrcData = new byte[length];

                if (file.Read(SrcData, 0, length) != length)
                    throw new Exception("Error reading");
            }

            using (var streamReader = new StreamReader(new MemoryStream(SrcData, false))) {
                Encoding = streamReader.CurrentEncoding;
                Lines = new List<string>();
                string line;

                while ((line = streamReader.ReadLine()) != null) {
                    Lines.Add(line);
                }
            }
        }
        private         void            _writeFile(string filename)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (StreamWriter streamWriter = new StreamWriter(memoryStream, Encoding, 512, true))
                {
                    foreach(var line in Lines) {
                        streamWriter.Write(line);
                        streamWriter.Write(EOL);
                    }
                }

                if (!_compare(SrcData, memoryStream)) {
                    Console.WriteLine(filename + ": cleaned.");

                    using (var file = new FileStream(filename, FileMode.Truncate, FileAccess.Write, FileShare.None))
                        file.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
            }
        }
        private static  bool            _compare(byte[] bfr, MemoryStream stream)
        {
            if (bfr.Length != stream.Length)
                return false;

            var sb = stream.GetBuffer();

            for (int i = 0 ; i < bfr.Length ; ++i) {
                if (bfr[i] != sb[i])
                    return false;
            }

            return true;
        }
    }
}