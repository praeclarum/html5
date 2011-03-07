using System;
using System.Text;

namespace Html5
{
	public class HCharacterData : HNode
	{
		StringBuilder _data;

		public string Data
		{
			get {
				return _data.ToString ();
			}
			set {
				_data = new StringBuilder (value);
			}
		}

		public override string OuterHtml {
			get {
				return Data;
			}
		}

		public void AppendData (string data)
		{
			_data.Append (data);
		}

		public void AppendData (char ch)
		{
			_data.Append (ch);
		}

		public HCharacterData (string data)
		{
			_data = new StringBuilder (data);
		}

		public HCharacterData (char ch)
		{
			_data = new StringBuilder ();
			_data.Append (ch);
		}

		public HCharacterData ()
		{
			_data = new StringBuilder ();
		}

		public override string ToString ()
		{
			return Data;
		}
	}
}

