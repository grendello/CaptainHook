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

namespace CaptainHook.Mail
{
	public abstract class TemplateFragment : CommonBase
	{
		StringBuilder builder;
		
		protected StringBuilder Builder {
			get {
				if (builder == null)
					builder = new StringBuilder ();

				return builder;
			}
		}
		
		public string InFile { get; set; }
		public int ColumnStart { get; set; }
		public int ColumnEnd { get; set; }
		public int LineStart {get; set; }
		public int LineEnd { get; set; }

		public TemplateFragment ()
		{
			this.LineStart = -1;
			this.ColumnStart = -1;
			this.LineEnd = -1;
			this.ColumnEnd = -1;
		}

		public virtual void Append (char ch)
		{
			Builder.Append (ch);
		}

		public virtual void Validate ()
		{
			if (String.IsNullOrEmpty (InFile))
				throw new InvalidOperationException ("InFile must not be null or empty.");
		}
	}
}
