using System;
using System.Collections.Generic;

namespace Html5
{
	public class HDocumentBuilder : ITokenObserver
	{
		Tokenizer _tokenizer;

		Token _currentToken;

		Action _insertMode;
		Action _origInsertMode;

		HDocument _doc;

		public HDocument Document { get { return _doc; } }

		Stack<HElement> _openElements;

		HElement _headElement;

		HElement CurrentNode {
			get { return (_openElements.Count > 0) ? _openElements.Peek () : null; }
		}

		HElement PopElement ()
		{
			return _openElements.Pop ();
		}

		void AcknowledgeSelfClosing (Token token)
		{
			var st = token as StartTagToken;
			if (st != null) {
				st.SelfClosingAcknowledged = true;
			}
		}

		HElement InsertElement (Token token)
		{
			var tag = token as TagToken;
			var name = (tag != null) ? tag.Name : "?";
			return InsertElement (name, tag);
		}

		HElement CreateElement (string name, Token token)
		{
			var elm = new HElement (name);
			var tag = token as TagToken;

			if (tag != null) {
			}

			return elm;
		}

		HElement InsertElement (string name, Token token)
		{
			var elm = CreateElement (name, token);

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
			_origInsertMode = Initial;
			_insertMode = Initial;

			_tokenizer.Subscribe (this);
			_tokenizer.Run ();
		}

		public void OnNext (Token token)
		{
			_currentToken = token;
			_insertMode ();
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
					InsertElement ("html", t);
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
				InsertElement ("html", t);
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
				_insertMode ();
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#parsing-main-inbody
		/// </summary>
		void InBody ()
		{
			throw new NotImplementedException ();
		}

		void InsertChar (int ch) {
			var cn = CurrentNode;

			if (cn != null) {
				var t = cn.LastChild as HText;
				if (t == null) {
					t = new HText ((char)ch);
					cn.AppendChild (t);
				}
				else {
					t.AppendData ((char)ch);
				}
			}
		}

		bool _scriptingFlag = true;

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#parsing-main-inhead
		/// </summary>
		void InHead ()
		{
			var handled = false;
			var t = _currentToken;
			var ch = t.Character;

			switch (t.Type)
			{
			case TokenType.Character:
				if (ch == '\t' || ch == '\r' || ch == '\n' || ch == ' ') {
					InsertChar (ch);
					handled = true;
				}
				break;
			case TokenType.Comment:
				CurrentNode.AppendChild (new HComment(((CommentToken)t).Data));
				handled = true;
				break;
			case TokenType.Doctype:
				ParseError ("Unexpected DOCTYPE in head.");
				handled = true;
				break;
			case TokenType.StartTag:
				switch (((TagToken)t).Name) {
				case "html":
					InBody ();
					handled = true;
					break;
				case "base":
				case "basefont":
				case "bgsound":
				case "command":
				case "link":
					InsertElement (t);
					PopElement ();
					AcknowledgeSelfClosing (t);
					handled = true;
					break;
				case "meta":
					InsertElement (t);
					PopElement ();
					AcknowledgeSelfClosing (t);
					handled = true;
					break;
				case "title":
					ParseGenericRcdataElement (t);
					handled = true;
					break;
				case "noscript":
				case "noframes":
				case "style":
					ParseGenericRawTextElement (t);
					handled = true;
					break;
				case "script":
					InsertElement ("script", t);
					_tokenizer.SetScriptDataState ();
					_origInsertMode = _insertMode;
					_insertMode = Text;
					break;
				}
				break;
			case TokenType.EndTag:
				break;
			}

			if (!handled) {
			}
		}

		void ParseGenericRcdataElement (Token t)
		{
			InsertElement (t);
			_tokenizer.SetRcdataState ();
			_origInsertMode = _insertMode;
			_insertMode = Text;
		}

		void ParseGenericRawTextElement (Token t)
		{
			InsertElement (t);
			_tokenizer.SetRawTextState ();
			_origInsertMode = _insertMode;
			_insertMode = Text;
		}

		void MarkScriptAlreadyStarted ()
		{
			var cn = CurrentNode;
			if (cn != null && cn.NodeName == "script") {
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#parsing-main-incdata
		/// </summary>
		void Text ()
		{
			var t = _currentToken;

			switch (t.Type) {
			case TokenType.Character:
				InsertChar (t.Character);
				break;
			case TokenType.EndOfFile:
				ParseError ("Unexpected EOF in text.");
				MarkScriptAlreadyStarted ();
				PopElement ();
				_insertMode = _origInsertMode;
				break;
			case TokenType.EndTag:
				switch (((TagToken)t).Name)
				{
				case "script":
				{
					var script = CurrentNode;
					throw new NotImplementedException ();
				}
					break;
				default:
					PopElement ();
					_insertMode = _origInsertMode;
					break;
				}
				break;
			}
		}
	}
}

