using System;

namespace Html5
{
	public class HComment : HCharacterData
	{
		public HComment ()
		{
		}

		public HComment (string data) : base (data)
		{
			Data = data;
		}
	}
}

