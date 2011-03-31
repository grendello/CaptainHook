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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace CaptainHook.Mail
{
	public class TemplateElementPropertyReference <TData> : TemplateElement
	{
		static readonly Type[] collectionTypes = {
			typeof(IEnumerable<>),
			typeof(ICollection<>),
			typeof(IList<>),
			typeof(IDictionary<,>)
		};

		PropertyInfo property;
		TemplateElement subProperty;
		
		string propertyName;
		string subPropertyName;

		public bool IsCollection {
			get; private set;
		}

		public Type PropertyType {
			get {
				if (property == null)
					return null;

				return property.PropertyType;
			}
		}

		public ITemplate Template {
			get; set;
		}

		public TemplateElementPropertyReference (string propertyName)
		{
			if (String.IsNullOrEmpty (propertyName))
				throw new ArgumentNullException ("propertyName");

			int dot = propertyName.IndexOf ('.');
			if (dot != -1) {
				this.propertyName = propertyName.Substring (0, dot);
				this.subPropertyName = propertyName.Substring (dot + 1);
			} else
				this.propertyName = propertyName;
			
			ResolveProperty ();
		}

		public override string Generate (object data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			if (data.GetType () != typeof(TData))
				throw new ArgumentException (String.Format ("must be an instance of the '{0}' type", typeof(TData)), "data");

			TData item = (TData)data;
			object value = property.GetValue (item, null);
			if (subProperty != null)
				return subProperty.Generate (value);

			if (value == null)
				return String.Empty;

			if (IsCollection) {
				ITemplate template = Template;

				var sb = new StringBuilder ();
				IEnumerable enumerable = value as IEnumerable;

				if (template != null) {
					foreach (object o in enumerable)
						sb.Append (template.ComposeMailBody (o));
				} else {
					foreach (object o in enumerable)
						if (o != null)
							sb.Append (o.ToString ());
				}

				return sb.ToString ();
			}

			// TODO: make the date format configurable (preferably right in the template, as a macro parameter)
			if (typeof (TData) == typeof (DateTime))
				return ((DateTime)value).ToString ("R");

			return value.ToString ();
		}

		void ResolveProperty ()
		{
			Type t = typeof(TData);
			PropertyInfo pi = t.GetProperty (propertyName, BindingFlags.Public | BindingFlags.Instance);
			if (pi == null)
				throw new InvalidOperationException (String.Format ("Public instance property '{0}' does not exist in type '{1}'", propertyName, t.FullName));

			if (!pi.CanRead)
				throw new InvalidOperationException (String.Format ("Property '{0}.{1}' is not readable.", t.FullName, propertyName));

			property = pi;
			Type pt = property.PropertyType;

			if (pt != typeof(string) && pt.IsGenericType) {
				Type[] interfaces = pt.GetInterfaces ();
				bool done = false;
				foreach (Type ct in collectionTypes) {
					foreach (Type iface in interfaces) {
						if (ct.IsAssignableFrom (iface) || (iface.IsGenericType && ct.IsAssignableFrom (iface.GetGenericTypeDefinition ()))) {
							IsCollection = done = true;
							break;
						}
					}

					if (done)
						break;
				}
			}
			if (String.IsNullOrEmpty (subPropertyName))
				return;

			Type myself = typeof (TemplateElementPropertyReference <>);
			myself = myself.MakeGenericType (property.PropertyType);
			subProperty = Activator.CreateInstance (myself, new object[] { subPropertyName }) as TemplateElement;
		}
	}
}
