using System;

namespace Html5
{
	public class HText : HCharacterData
	{
		public HText (char ch) : base (ch)
		{
		}

		public override string ToString ()
		{
			return Data;
		}
	}
}

