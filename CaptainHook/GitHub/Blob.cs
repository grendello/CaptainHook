// 
//  Authors:
//    Marek Habersack grendel@twistedcode.net
// 
//  Copyright (c) 2010, Novell, Inc (http://novell.com/)
// 
//  All rights reserved.
// 
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * Neither the name of Novell, Inc nor names of the contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Text;

using CaptainHook.Base;

namespace CaptainHook.GitHub
{
	public class Blob : CommonBase
	{
		string diff;

		// GitHub members
		public string Data { get; set; }
		public string Mime_Type { get; set; }
		public string Mode { get; set; }
		public string Name { get; set; }
		public int Size { get; set; }
		public string SHA { get; set; }

		// Our members
		public string Diff {
			get {
				if (diff == null)
					diff = ConvertToDiff ();

				return diff;
			}
		}

		string ConvertToDiff ()
		{
			if (Mime_Type != "text/plain")
				return String.Empty;

			string data = Data;
			if (String.IsNullOrEmpty (data))
				return String.Empty;

			string[] lines = Data.Split (new char[] { '\n' });
			if (lines == null || lines.Length == 0)
				return String.Empty;

			var sb = new StringBuilder ();
			sb.Append ("--- /dev/null");
			sb.AppendLine ();
			sb.AppendFormat ("+++ b/{0}", Name);
			sb.AppendLine ();
			sb.AppendFormat ("@@ -0,0 +1,{0} @@", lines.Length);
			sb.AppendLine ();

			foreach (string line in lines) {
				sb.Append ("+" + line);
				sb.AppendLine ();
			}

			return sb.ToString ();
		}
	}
}

