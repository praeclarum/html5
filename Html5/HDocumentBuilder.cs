using System;
using System.Collections.Generic;

namespace Html5
{
	public class HDocumentBuilder
	{
		Tokenizer _tokenizer;

		Token _currentToken;

		Action _insertMode;

		HDocument _doc;

		public HDocument Document { get { return _doc; } }

		Stack<HElement> _openElements;

		HElement _headElement;

		HNode CurrentNode {
			get { return (_openElements.Count > 0) ? _openElements.Peek () : null; } }

		HElement InsertElement (string name, Token token)
		{
			var elm = new HElement (name);
			var tag = token as TagToken;

			if (tag != null) {
			}

			var cn = CurrentNode;
			if (cn == null) {
				_doc.AppendChild (elm);
			}
			else {
				cn.AppendChild (elm);
			}
			_openElements.Push (elm);
			return elm;
		}

		public HDocumentBuilder (System.IO.TextReader reader)
		{
			_tokenizer = new Tokenizer (reader);
			_openElements = new Stack<HElement> ();
			_doc = new HDocument ();
			_insertMode = Initial;

			while (_currentToken == null || _currentToken.Type != TokenType.EndOfFile) {
				var toks = _tokenizer.GetNextTokens ();
				foreach (var t in toks) {
					_currentToken = t;
					_insertMode ();
				}
			}
		}

		void Initial ()
		{
			var t = _currentToken;
			char ch = t.Character;

			switch (t.Type)
			{
			case TokenType.Character:
				if (ch == '\t' || ch == '\r' || ch == '\n' || ch == ' ') {
					// Ignore
				}
				else {
					_insertMode = BeforeHtml;
					_insertMode ();
				}
				break;
			case TokenType.Comment:
				_doc.AppendChild (new HComment (((CommentToken)t).Data));
				break;
			case TokenType.Doctype:
				_doc.AppendChild (new HDocumentType () {
					Name = ((DoctypeToken)t).Name,
					PublicId = ((DoctypeToken)t).PublicId,
					SystemId = ((DoctypeToken)t).SystemId,
				});
				_insertMode = BeforeHtml;
				break;
			default:
				_insertMode = BeforeHtml;
				break;
			}
		}

		void ParseError (string error)
		{
			System.Console.Error.WriteLine ("! " + error);
		}

		void BeforeHtml ()
		{
			var t = _currentToken;
			char ch = t.Character;

			var handled = false;

			switch (t.Type)
			{
			case TokenType.Doctype:
				ParseError ("Unexpected DOCTYPE before HTML.");
				handled = true;
				break;
			case TokenType.Comment:
				_doc.AppendChild (new HComment (((CommentToken)t).Data));
				handled = true;
				break;
			case TokenType.Character:
				if (ch == '\t' || ch == '\r' || ch == '\n' || ch == ' ') {
					// Ignore
					handled = true;
				}
				break;
			case TokenType.StartTag:
				if (((TagToken)t).Name == "html") {
					var elm = InsertElement ("html", t);
					_insertMode = BeforeHead;
					handled = true;
				}
				break;
			case TokenType.EndTag:
				switch (((TagToken)t).Name) {
				case "head":
				case "body":
				case "html":
				case "br":
					break;
				default:
					ParseError ("Unexpected end tag: " + t);
					// Ignore
					handled = true;
					break;
				}
				break;
			}

			if (!handled) {
				var elm = InsertElement ("html", t);
				_insertMode = BeforeHead;
				handled = true;
			}
		}

		void BeforeHead ()
		{
			var handled = false;
			var t = _currentToken;
			var ch = t.Character;

			switch (t.Type)
			{
			case TokenType.Character:
				if (ch == '\t' || ch == '\r' || ch == '\n' || ch == ' ') {
					// Ignore
					handled = true;
				}
				break;
			case TokenType.Comment:
				CurrentNode.AppendChild (new HComment (((CommentToken)t).Data));
				handled = true;
				break;
			case TokenType.Doctype:
				ParseError ("Unexpected DOCTYPE before head.");
				handled = true;
				break;
			case TokenType.StartTag:
				switch (((TagToken)t).Name) {
				case "html":
					InBody ();
					handled = true;
					break;
				case "head":
					_headElement = InsertElement ("head", t);
					_insertMode = InHead;
					handled = true;
					break;
				}
				break;
			case TokenType.EndTag:
				switch (((TagToken)t).Name) {
				case "head":
				case "body":
				case "html":
				case "br":
					break;
				default:
					ParseError ("Unexpexted end tag: " + t);
					// Ignore
					handled = true;
					break;
				}
				break;
			}

			if (!handled) {
				_headElement = InsertElement ("head", null);
				_insertMode = InHead;
			}
		}

		void InBody ()
		{
			throw new NotImplementedException ();
		}

		void InHead ()
		{
			throw new NotImplementedException ();
			var t = _currentToken;
			switch (t.Type)
			{
			case TokenType.Character:
				break;
			case TokenType.Comment:
				break;
			case TokenType.Doctype:
				break;
			case TokenType.StartTag:
				break;
			case TokenType.EndTag:
				break;
			default:
				//Act as if an end tag token with the tag name "head" had been seen, and reprocess the current token.
				throw new NotImplementedException();
			}
		}
	}
}

