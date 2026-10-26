// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace EftPosition
{
    // Canonical absolute subpaths avoid legacy renderer state leaking across
    // relative moveto commands after closepath. Geometry is preserved.
    public static class SvgPathCanonicalizer
    {
        sealed class Reader
        {
            readonly string input;
            int index;
            public Reader(string text) { input=text; }
            void Space() { while(index<input.Length && (Char.IsWhiteSpace(input[index]) || input[index]==',')) index++; }
            public bool End { get { Space(); return index>=input.Length; } }
            public bool Command { get { Space(); return index<input.Length && Char.IsLetter(input[index]); } }
            public char ReadCommand() { Space(); return input[index++]; }
            public double Number()
            {
                Space(); int first=index;
                if(index<input.Length && (input[index]=='+' || input[index]=='-')) index++;
                bool digit=false;
                while(index<input.Length && input[index]>='0' && input[index]<='9') {index++;digit=true;}
                if(index<input.Length && input[index]=='.')
                { index++; while(index<input.Length && input[index]>='0' && input[index]<='9') {index++;digit=true;} }
                if(!digit) throw new InvalidDataException("Nieprawidłowa liczba w geometrii SVG.");
                if(index<input.Length && (input[index]=='e' || input[index]=='E'))
                {
                    index++; if(index<input.Length && (input[index]=='+' || input[index]=='-')) index++;
                    int exponent=index;
                    while(index<input.Length && input[index]>='0' && input[index]<='9') index++;
                    if(index==exponent) throw new InvalidDataException("Nieprawidłowy wykładnik SVG.");
                }
                double value=Double.Parse(input.Substring(first,index-first),CultureInfo.InvariantCulture);
                if(Double.IsNaN(value) || Double.IsInfinity(value)) throw new InvalidDataException("Nieprawidłowa współrzędna SVG.");
                return value;
            }
            public double Flag()
            {
                Space(); if(index>=input.Length || (input[index]!='0' && input[index]!='1')) throw new InvalidDataException("Nieprawidłowa flaga łuku SVG.");
                return input[index++]-'0';
            }
        }
        static void Emit(StringBuilder output,char command,params double[] values)
        {
            if(output.Length>0) output.Append(' ');
            output.Append(command);
            foreach(double value in values) {output.Append(' ');output.Append(value.ToString("R",CultureInfo.InvariantCulture));}
        }
        public static string Convert(string input)
        {
            if(String.IsNullOrWhiteSpace(input)) return input;
            Reader r=new Reader(input); StringBuilder output=new StringBuilder(input.Length+64);
            char command='\0',previous='\0';
            double x=0,y=0,startX=0,startY=0,cubicX=0,cubicY=0,quadX=0,quadY=0;
            while(!r.End)
            {
                if(r.Command) command=r.ReadCommand();
                if(command=='\0') throw new InvalidDataException("Brak polecenia ścieżki SVG.");
                bool relative=Char.IsLower(command); char upper=Char.ToUpperInvariant(command);
                double nx,ny,a,b,c,d;
                switch(upper)
                {
                    case 'Z':
                        Emit(output,'Z');x=startX;y=startY;previous='Z';command='\0';continue;
                    case 'M':
                        nx=r.Number();ny=r.Number();if(relative){nx+=x;ny+=y;}
                        Emit(output,'M',nx,ny);x=startX=nx;y=startY=ny;
                        command=relative?'l':'L';previous='M';continue;
                    case 'L':
                        nx=r.Number();ny=r.Number();if(relative){nx+=x;ny+=y;}
                        Emit(output,'L',nx,ny);x=nx;y=ny;break;
                    case 'H':
                        nx=r.Number();if(relative)nx+=x;Emit(output,'L',nx,y);x=nx;break;
                    case 'V':
                        ny=r.Number();if(relative)ny+=y;Emit(output,'L',x,ny);y=ny;break;
                    case 'C':
                        a=r.Number();b=r.Number();c=r.Number();d=r.Number();nx=r.Number();ny=r.Number();
                        if(relative){a+=x;b+=y;c+=x;d+=y;nx+=x;ny+=y;}
                        Emit(output,'C',a,b,c,d,nx,ny);cubicX=c;cubicY=d;x=nx;y=ny;break;
                    case 'S':
                        a=(previous=='C' || previous=='S')?2*x-cubicX:x;
                        b=(previous=='C' || previous=='S')?2*y-cubicY:y;
                        c=r.Number();d=r.Number();nx=r.Number();ny=r.Number();
                        if(relative){c+=x;d+=y;nx+=x;ny+=y;}
                        Emit(output,'C',a,b,c,d,nx,ny);cubicX=c;cubicY=d;x=nx;y=ny;break;
                    case 'Q': case 'T':
                        if(upper=='Q') { a=r.Number();b=r.Number();if(relative){a+=x;b+=y;} }
                        else { a=(previous=='Q' || previous=='T')?2*x-quadX:x;b=(previous=='Q' || previous=='T')?2*y-quadY:y; }
                        nx=r.Number();ny=r.Number();if(relative){nx+=x;ny+=y;}
                        Emit(output,'C',x+(a-x)*2/3,y+(b-y)*2/3,nx+(a-nx)*2/3,ny+(b-ny)*2/3,nx,ny);
                        quadX=a;quadY=b;x=nx;y=ny;break;
                    case 'A':
                        a=Math.Abs(r.Number());b=Math.Abs(r.Number());c=r.Number();d=r.Flag();double sweep=r.Flag();nx=r.Number();ny=r.Number();
                        if(relative){nx+=x;ny+=y;}
                        if(a==0 || b==0) Emit(output,'L',nx,ny); else Emit(output,'A',a,b,c,d,sweep,nx,ny);
                        x=nx;y=ny;break;
                    default: throw new InvalidDataException("Nieobsługiwane polecenie SVG: "+command);
                }
                previous=upper;
            }
            return output.ToString();
        }
    }
}
