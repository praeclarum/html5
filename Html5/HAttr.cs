using System;

namespace Html5
{
	public class HAttr
	{
		public string Name { get; set; }
		public string Value { get; set; }

		public HAttr ()
		{
		}

		public HAttr (string name, string value)
		{
			Name = name;
			Value = value;
		}
	}
}

