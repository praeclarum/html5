using System;
using System.Collections.Generic;
using System.Linq;

namespace Html5
{
	public class HNode
	{
		List<HNode> _childNodes;

		public IEnumerable<HNode> ChildNodes {
			get { return _childNodes ?? Enumerable.Empty<HNode> (); }
		}

		public string NodeName { get; set; }

		public HNode ()
		{
			NodeName = "";
		}

		public HNode (string name)
		{
			NodeName = name;
		}

		public void AppendChild (HNode newChild)
		{
			if (_childNodes == null) {
				_childNodes = new List<HNode> ();
			}
			_childNodes.Add (newChild);
		}
	}
}

