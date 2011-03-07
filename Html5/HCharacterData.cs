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

		public HCharacterData (string data)
		{
			_data = new StringBuilder (data);
		}

		public HCharacterData ()
		{
			_data = new StringBuilder ();
		}
	}
}

