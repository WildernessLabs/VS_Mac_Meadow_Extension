using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac.Gui
{

    /// <summary>
    /// Order text into columns, using a monospaced font.
    /// </summary>
    public class TextTableDisplay
    {
        List<object> Lines = new List<object>();
        List<string> Columns = new List<string>();
        List<int> ColumnMaxLen = new List<int>();
        int colCountMax = -1;
        StringBuilder sb = new StringBuilder();
    
        public int Margin {get; private set;}
    
    
        public char ColumnChar {get; set;} = '|';
        
    
        public TextTableDisplay(int margin = 1)
        {
            Margin = margin;
        }
        
        /// <summary>
        /// Append columns across existing line, automatic new line
        /// </summary>
        /// <param name="text">Text.</param>
        public void AppendCol(params String[] text)
        {
            foreach(var item in text)
            {
                AppendCol(item);
            }
            
            while( Columns.Count < colCountMax) //If we have more columms previously, add blanks.
            {
                AppendCol("");
            }

            NewLine();
        }

        /// <summary>
        /// Append column across existing line. No new line
        /// </summary>
        public void AppendCol(String text)
        {
            Columns.Add(text);
            
            if (Columns.Count > colCountMax) colCountMax = Columns.Count;
            
            if (ColumnMaxLen.Count < Columns.Count)
              ColumnMaxLen.Add(text.Length);
            else if (ColumnMaxLen[Columns.Count-1] < text.Length)
              ColumnMaxLen[Columns.Count-1] = text.Length;
        }
        
        
        /// <summary>
        /// New Line of text, ignoring columns
        /// </summary>
        public void AppendLine(String text="")
        {
            NewLine();
            Lines.Add(text);
        }
        
        /// <summary>
        /// Append length of table with a single character
        /// </summary>
        /// <param name="linechar">Linechar.</param>
        public void AppendCharLine(char linechar)
        {
            NewLine();
            Lines.Add(linechar);
        }
        
        /// <summary>
        /// Append text at cursor. No new line
        /// </summary>
        /// <param name="text">Text.</param>
        public void Append(string text)
        {
            if (Columns.Count==0)
            {
                if (Lines.Count==0)
                {
                    sb.Append(text);
                }
                else
                {
                    if (Lines[Lines.Count-1] is String[])
                    {
                        string[] cols = (string[])Lines[Lines.Count-1];
                        cols[cols.Length-1] += text;
                    }
                    else if (Lines[Lines.Count-1] is String)
                    {
                        Lines[Lines.Count-1] = Lines[Lines.Count-1]+text;
                    }
                }
            }
            else
            {
                Columns[Columns.Count-1] = Columns[Columns.Count-1]+text;
            }
        }
        
        /// <summary>
        /// Force a new line
        /// </summary>
        public void NewLine()
        {
            if (Columns.Count>0)
            {
                Lines.Add(Columns.ToArray());
                Columns.Clear();
            }
        }
        
        public new string ToString()
        {
            foreach (var line in Lines)
            {
                if (line is String[])
                {
                    for (int f=0; f < colCountMax;f++)
                    {
                        if (f < ((string[])line).Length)
                        {
                            sb.Append( ((string[])line)[f].PadRight(ColumnMaxLen[f]+Margin));
                            if (f < colCountMax-1) sb.Append($"{ColumnChar} ");
                        }
                        else
                        {
                            sb.Append(new string(' ',ColumnMaxLen[f]+Margin));
                        }
                    }
                    sb.AppendLine();
                }
                else if (line is String)
                {
                    sb.AppendLine((string)line);
                }
                else if (line is char)
                {
                    for (int f=0; f < colCountMax;f++)
                    {
                        sb.Append(new string((char)line,ColumnMaxLen[f]+Margin));
                        if (f > 0)sb.Append(line);
                        if (f < colCountMax-1) sb.Append(ColumnChar);
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }        
    }
}
