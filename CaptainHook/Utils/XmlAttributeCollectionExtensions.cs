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
using System.ComponentModel;
using System.Xml;

namespace CaptainHook.Utils
{
	public static class XmlAttributeCollectionExtensions
	{
		public static T GetRequired <T> (this XmlAttributeCollection coll, string name)
		{
			T value = coll.GetOptional <T> (name, default (T));

			if (value == null)
				throw new InvalidOperationException (String.Format ("Required attribute '{0}' not found", name));

			return value;
		}

		public static T GetOptional <T> (this XmlAttributeCollection coll, string name, T deflt)
		{
			string value = null;
			
			if (coll != null) {
				XmlAttribute attr = coll [name];
				if (attr != null)
					value = attr.Value;
			}

			if (value == null)
				return deflt;

			if (typeof (T) == typeof (string))
				return (T)((object)value);
			
			TypeConverter cvt = TypeDescriptor.GetConverter (typeof (T));
			if (cvt == null)
				throw new InvalidOperationException (String.Format ("Unable to find type converter for '{0}'", typeof (T).FullName));

			if (!cvt.CanConvertFrom (typeof (string)))
				throw new InvalidOperationException (String.Format ("Conversion from string to '{0}' is not supported", typeof (T).FullName));
			
			return (T)cvt.ConvertFrom (value);
		}
	}
}
