// 
//  Author:
//    Marek Habersack grendel@twistedcode.net
// 
//  Copyright (c) 2011, Marek Habersack
// 
//  All rights reserved.
// 
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * Neither the name of Marek Habersack nor names of the contributors may be used to endorse or promote products derived from this software without specific prior written permission.
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
using System.Collections.Generic;
using System.Web;

using CaptainHook.Utils;

namespace CaptainHook.Web.Processing
{
	sealed class SenderState
	{
		object ignoresLock = new object ();
		Dictionary <string, bool> ignoredFiles;
		public string CsDataDir { get; set; }
		public string CommitSourceID { get; set; }
		public JsonDeserializer Deserializer { get; set; }

		public void Ignore (string filePath)
		{
			lock (ignoresLock) {
				if (ignoredFiles == null)
					ignoredFiles = new Dictionary<string, bool> (StringComparer.Ordinal);
				if (ignoredFiles.ContainsKey (filePath))
					return;
				ignoredFiles.Add (filePath, true);
			}
		}

		public bool IsIgnored (string filePath)
		{
			lock (ignoresLock) {
				if (ignoredFiles == null || ignoredFiles.Count == 0)
					return false;
				return ignoredFiles.ContainsKey (filePath);
			}
		}
	}
}

