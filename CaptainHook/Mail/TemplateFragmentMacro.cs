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
using System.Collections.Generic;
using System.Text;

namespace CaptainHook.Mail
{
	public class TemplateFragmentMacro : TemplateFragment
	{
		List <TemplateFragmentArgument> arguments;
		
		public string MacroName {
			get { return Builder.ToString (); }
		}
		
		public List <TemplateFragmentArgument> Arguments {
			get {
				if (arguments == null)
					arguments = new List <TemplateFragmentArgument> ();

				return arguments;
			}
		}

		public bool HasArguments {
			get { return arguments != null && arguments.Count > 0; }
		}

		public TemplateFragmentMacro ()
		{}

		public TemplateFragmentMacro (string inputFile)
		{
			if (String.IsNullOrEmpty (inputFile))
				throw new ArgumentNullException ("inputFile");
			
			this.InFile = inputFile;
		}

		public override string ToString ()
		{
			return String.Format ("{0} (@{1}@) [{2} arguments]", this.GetType ().FullName, MacroName, arguments == null ? "no" : arguments.Count.ToString ());
		}

		public override void Validate ()
		{
			base.Validate ();
			if (String.IsNullOrEmpty (MacroName))
				throw new InvalidOperationException ("MacroName must not be null or empty.");
		}
	}
}
