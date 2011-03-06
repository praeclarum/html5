using System;
using System.Text;

namespace Html5
{
	public class Attribute
	{
		StringBuilder _name;
		StringBuilder _value;

		public string Name { get { return _name.ToString (); } }
		public string Value { get { return _value.ToString (); } }

		public Attribute (string name) {
			_name = new StringBuilder ();
			_name.Append (name);
			_value = new StringBuilder ();
		}

		public Attribute (int ch) {
			_name = new StringBuilder();
			_name.Append ((char)ch);
			_value = new StringBuilder ();
		}

		public void AppendName (int ch) {
			_name.Append ((char)ch);
		}

		public void AppendValue (int ch) {
			_value.Append ((char)ch);
		}

		public override string ToString ()
		{
			return Name + " = " + Value;
		}
	}
}
