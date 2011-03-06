using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Html5.Tokenizer
{
	public class Tokenizer
	{
		TextReader _reader;
		
		Action _state;
		
		int _currentInputChar;

		TagToken _currentTag = null;
		DoctypeToken _currentDoctypeToken = null;
		CommentToken _currentComment = null;

		int _additionalAllowedChar = -2;

		bool _isEof = false;

		public Tokenizer (TextReader reader)
		{
			if (reader == null) throw new ArgumentNullException ("reader");
			_reader = reader;
		}

		public void Run ()
		{
			_state = Data;
			while (!_isEof) {
				ConsumeNextInputChar ();
				_state ();
			}
		}
		
		int ConsumeNextInputChar ()
		{
			_currentInputChar = _reader.Read ();
			return _currentInputChar;
		}
		
		void Emit (Token token)
		{
			if (token.Type == TokenType.EndOfFile) {
				_isEof = true;
			}
			Console.Write (token);
		}
		
		void ParseError (string message)
		{
			Console.WriteLine ("! " + message);
		}
		
		#region Parse States

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#data-state
		/// </summary>
		void Data ()
		{
			switch (_currentInputChar) {
			case '&':
				_state = CharacterReferenceInData;
				break;
			case '<':
				_state = TagOpen;
				break;
			case '\u0000':
				ParseError ("Unexpected NULL character.");
				Emit (Token.CharacterToken (_currentInputChar));
				break;
			case -1:
				Emit (Token.EndOfFileToken ());
				break;
			default:
				Emit (Token.CharacterToken (_currentInputChar));
				break;
			}
		}

		/// <summary>
		/// Characters the reference in data.
		/// </summary>
		void CharacterReferenceInData ()
		{
			_additionalAllowedChar = -2;
			ConsumeCharacterReference ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#consume-a-character-reference
		/// </summary>
		void ConsumeCharacterReference ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
			case '<':
			case '&':
			case -1:
				// Not a character reference. No characters are consumed, and nothing is returned. (This is not an error, either.)
				break;
			case '#':
				ch = ConsumeNextInputChar ();
				if (ch == 'x' || ch == 'X') {
					ConsumeHexCharacterReference ();
				}
				else {
					ConsumeDecCharacterReference ();
				}
				break;
			default:
				if (ch == _additionalAllowedChar) {
					// Not a character reference. No characters are consumed, and nothing is returned. (This is not an error, either.)
				}
				else {
					throw new NotImplementedException ("Named Ref");
				}
				break;
			}
		}

		void ConsumeDecCharacterReference ()
		{
			throw new NotImplementedException ();
		}

		void ConsumeHexCharacterReference ()
		{
			throw new NotImplementedException ();
		}

		void TagOpen ()
		{
			var ch = _currentInputChar;
			
			if (ch == '!') {
				_state = MarkupDeclarationOpen;
			}
			else if (ch == '/') {
				_state = EndTagOpen;
			}
			else if (('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z')) {
				_currentTag = new StartTagToken (ch);
				_state = TagName;
			}
			else if (ch == '?') {
				ParseError ("Bogus comment.");
				_state = BogusComment;
			}
			else {
				ParseError ("Expected tag name.");
				Emit (Token.CharacterToken ('<'));
				_state = Data;
				_state ();
			}
		}
		
		void TagName ()
		{
			switch (_currentInputChar)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BeforeAttributeName;
				break;
			case '/':
				_state = SelfClosingStartTag;
				break;
			case '>':
				_state = Data;
				Emit (_currentTag);
				_currentTag = null;
				break;
			case '\u0000':
				ParseError ("Unexpected NULL in tag name.");
				_currentTag.AppendName ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in tag name.");
				_state = Data;
				_state ();
				break;
			default:
				_currentTag.AppendName (_currentInputChar);
				break;
			}
		}

		void BeforeAttributeName ()
		{
			Attribute attr;

			switch (_currentInputChar)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Do nothing
				break;
			case '/':
				_state = SelfClosingStartTag;
				break;
			case '>':
				_state = Data;
				Emit (_currentTag);
				break;
			case '\u0000':
				ParseError ("Unexpected NULL before attribute name.");
				attr = new Attribute ("\uFFFD");
				_currentTag.AddAttribute (attr);
				_state = AttributeName;
				break;
			case -1:
				ParseError ("Unexpected EOF before attribute name.");
				_state = Data;
				_state ();
				break;
			case '\"':
			case '\'':
			case '<':
			case '=':
				ParseError ("Unexpected `" + _currentInputChar + "` before attribute name");
				attr = new Attribute (_currentInputChar);
				_currentTag.AddAttribute (attr);
				_state = AttributeName;
				break;
			default:
				attr = new Attribute (_currentInputChar);
				_currentTag.AddAttribute (attr);
				_state = AttributeName;
				break;
			}
		}

		void AttributeName ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = AfterAttributeName;
				break;
			case '/':
				_state = SelfClosingStartTag;
				break;
			case '=':
				_state = BeforeAttributeValue;
				break;
			case '>':
				Emit (_currentTag);
				_state = Data;
				break;
			case '\u0000':
				ParseError ("Unexpected NULL in attribute name.");
				_currentTag.CurrentAttribute.AppendName ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in attribute name.");
				_state = Data;
				_state ();
				break;
			case '\"':
			case '\'':
			case '<':
				ParseError ("Unexpected `" + (char)ch + "` in attribute name");
				_currentTag.CurrentAttribute.AppendName (ch);
				break;
			default:
				if ('A' <= ch && ch <= 'Z') {
					ch = char.ToLowerInvariant ((char)ch);
				}
				_currentTag.CurrentAttribute.AppendName (ch);
				break;
			}
		}

		void AfterAttributeName ()
		{
			var ch = _currentInputChar;
			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Ignore the ch
				break;
			case '/':
				_state = SelfClosingStartTag;
				break;
			case '=':
				_state = BeforeAttributeValue;
				break;
			case '>':
				_state = Data;
				Emit (_currentTag);
				break;
			case '\u0000':
				ParseError ("Unexpected NULL after attribute name.");
				_currentTag.AddAttribute (new Attribute ('\uFFFD'));
				_state = AttributeName;
				break;
			case -1:
				ParseError ("Unexpected EOF after attribute name.");
				_state = Data;
				_state ();
				break;
			case '\"':
			case '\'':
			case '<':
				ParseError ("Unexpected `" + (char)ch + "` after attribute name");
				_currentTag.AddAttribute (new Attribute (ch));
				_state = AttributeName;
				break;
			default:
				if ('A' <= ch && ch <= 'Z') {
					ch = char.ToLowerInvariant ((char)ch);
				}
				_currentTag.AddAttribute (new Attribute (ch));
				_state = AttributeName;
				break;
			}
		}

		void BeforeAttributeValue ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Ignore the ch
				break;
			case '\"':
				_state = AttributeValueDoubleQuoted;
				break;
			case '&':
				_state = AttributeValueUnquoted;
				break;
			case '\'':
				_state = AttributeValueSingleQuoted;
				break;
			case '>':
				ParseError ("Unexpected `>` before attribute value.");
				_state = Data;
				Emit (_currentTag);
				break;
			case '\u0000':
				ParseError ("Unexpected NULL before attribute value.");
				_currentTag.CurrentAttribute.AppendValue ('\uFFFD');
				_state = AttributeName;
				break;
			case -1:
				ParseError ("Unexpected EOF before attribute value.");
				_state = Data;
				_state ();
				break;
			case '<':
			case '=':
			case '`':
				ParseError ("Unexpected `" + (char)ch + "` before attribute value");
				_currentTag.CurrentAttribute.AppendValue (ch);
				_state = AttributeValueUnquoted;
				break;
			default:
				_currentTag.CurrentAttribute.AppendValue (ch);
				_state = AttributeValueUnquoted;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#attribute-value-double-quoted-state
		/// </summary>
		void AttributeValueDoubleQuoted ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\"':
				_state = AfterAttributeValueQuoted;
				break;
			case '&':
				_additionalAllowedChar = -2;
				_state = CharacterReferenceInAttributeValue;
				break;
			case 0:
				ParseError ("Unexpected NULL in attribute value.");
				_currentTag.CurrentAttribute.AppendValue ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in attribute value.");
				_state = Data;
				_state ();
				break;
			default:
				_currentTag.CurrentAttribute.AppendValue (ch);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#attribute-value-unquoted-state
		/// </summary>
		void AttributeValueUnquoted ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BeforeAttributeName;
				break;
			case '&':
				_additionalAllowedChar = '>';
				_state = CharacterReferenceInAttributeValue;
				break;
			case '>':
				_state = Data;
				Emit (_currentTag);
				break;
			case 0:
				ParseError ("Unexpected NULL in attribute value.");
				_currentTag.CurrentAttribute.AppendValue ('\uFFFD');
				break;
			case '\"':
			case '\'':
			case '<':
			case '=':
			case '`':
				ParseError ("Unexpected `" + (char)ch + "` in attribute value.");
				_currentTag.CurrentAttribute.AppendValue (ch);
				break;
			case -1:
				ParseError ("Unexpected EOF in attribute value.");
				_state = Data;
				_state ();
				break;
			default:
				_currentTag.CurrentAttribute.AppendValue (ch);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#attribute-value-single-quoted-state
		/// </summary>
		void AttributeValueSingleQuoted ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\'':
				_state = AfterAttributeValueQuoted;
				break;
			case '&':
				_additionalAllowedChar = -2;
				_state = CharacterReferenceInAttributeValue;
				break;
			case 0:
				ParseError ("Unexpected NULL in attribute value.");
				_currentTag.CurrentAttribute.AppendValue ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in attribute value.");
				_state = Data;
				_state ();
				break;
			default:
				_currentTag.CurrentAttribute.AppendValue (ch);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#character-reference-in-attribute-value-state
		/// </summary>
		void CharacterReferenceInAttributeValue ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#after-attribute-value-quoted-state
		/// </summary>
		void AfterAttributeValueQuoted ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BeforeAttributeName;
				break;
			case '/':
				_state = SelfClosingStartTag;
				break;
			case '>':
				_state = Data;
				Emit (_currentTag);
				break;
			case -1:
				ParseError ("Unexpected EOF after attribute value.");
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` after attribute value.");
				_state = BeforeAttributeName;
				_state ();
				break;
			}
		}

		void SelfClosingStartTag ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#end-tag-open-state
		/// </summary>
		void EndTagOpen ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '>':
				ParseError ("Unexpected `>` in end tag.");
				_state = Data;
				break;
			case -1:
				ParseError ("Unexpected EOF in end tag.");
				Emit (Token.CharacterToken ('<'));
				Emit (Token.CharacterToken ('/'));
				_state = Data;
				_state ();
				break;
			default:
				if (('a' <= ch && ch <= 'z') ||
					('A' <= ch && ch <= 'Z')) {
					_currentTag = new EndTagToken (ch);
					_state = TagName;
				}
				else {
					_state = BogusComment;
				}
				break;
			}
		}

		static int[] _doctype = new int[] {
			'D', 'd', 'O', 'o', 'C', 'c', 'T', 't', 'Y', 'y', 'P', 'p', 'E', 'e'
		};

		bool MatchI (int[] chars) {
			var doctype = true;
			var i = 2;
			var ch = _currentInputChar;
			while (doctype && i < chars.Length) {
				ch = ConsumeNextInputChar ();
				doctype = (chars[i] == ch) || (chars[i+1] == ch);
				i += 2;
			}
			return doctype;
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#markup-declaration-open-state
		/// </summary>
		void MarkupDeclarationOpen ()
		{
			var ch = _currentInputChar;

			if (ch == '-') {
				ch = ConsumeNextInputChar ();
				if (ch == '-') {
					_currentComment = new CommentToken ();
					_state = CommentStart;
				}
				else {
					ParseError ("Unexpected `" + (char)ch + "` in opening of comment.");
					_state = BogusComment;
				}
			}
			else if (ch == 'd' || ch == 'D') {

				if (MatchI (_doctype)) {
					_state = Doctype;
				}
				else {
					ParseError ("Unexpected `" + (char)ch + "` in DOCTYPE of markup declaration.");
					_state = BogusComment;
				}
			}
			else if (ch == '[') {
				throw new NotImplementedException ("CDATA");
			}
			else {
				ParseError ("Unexpected `" + (char)ch + "` in opening of markup declaration.");
				_state = BogusComment;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-start-state
		/// </summary>
		void CommentStart ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '-':
				_state = CommentStartDash;
				break;
			case 0:
				ParseError ("Unexpected NULL before COMMENT.");
				_state = Comment;
				_currentComment.AppendData ('\uFFFD');
				break;
			case '>':
				ParseError ("Unexpected `>` before COMMENT.");
				_state = Data;
				Emit (_currentComment);
				break;
			case -1:
				ParseError ("Unexpected EOF before COMMENT.");
				_state = Data;
				_state ();
				break;
			default:
				_currentComment.AppendData (ch);
				_state = Comment;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-start-dash-state
		/// </summary>
		void CommentStartDash ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-end-state
		/// </summary>
		void CommentEnd ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '>':
				_state = Data;
				Emit (_currentComment);
				break;
			case 0:
				ParseError ("Unexpected NULL in COMMENT end.");
				_currentComment.AppendData ('-');
				_currentComment.AppendData ('-');
				_currentComment.AppendData ('\uFFFD');
				_state = Comment;
				break;
			case '!':
				ParseError ("Unexpected `!` in COMMENT end.");
				_state = CommentEndBang;
				break;
			case '-':
				ParseError ("Unexpected `-` in COMMENT end.");
				_currentComment.AppendData ('-');
				break;
			case -1:
				ParseError ("Unexpected EOF in COMMENT end.");
				Emit (_currentComment);
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` in COMMENT end.");
				_currentComment.AppendData ('-');
				_currentComment.AppendData ('-');
				_currentComment.AppendData (ch);
				_state = Comment;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-end-bang-state
		/// </summary>
		void CommentEndBang ()
		{
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-end-dash-state
		/// </summary>
		void CommentEndDash ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '-':
				_state = CommentEnd;
				break;
			case 0:
				ParseError ("Unexpected NULL in COMMENT.");
				_currentComment.AppendData ('-');
				_currentComment.AppendData ('\uFFFD');
				_state = Comment;
				break;
			case -1:
				ParseError ("Unexpected EOF in COMMENT.");
				Emit (_currentComment);
				_state = Data;
				_state ();
				break;
			default:
				_currentComment.AppendData ('-');
				_currentComment.AppendData (ch);
				_state = Comment;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#comment-state
		/// </summary>
		void Comment ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '-':
				_state = CommentEndDash;
				break;
			case 0:
				ParseError ("Unexpected NULL in COMMENT.");
				_currentComment.AppendData ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in COMMENT.");
				Emit (_currentComment);
				_state = Data;
				_state ();
				break;
			default:
				_currentComment.AppendData (ch);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-state
		/// </summary>
		void Doctype ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BeforeDoctypeName;
				break;
			case -1:
				ParseError ("Unexpected EOF in DOCTYPE.");
				Emit (new DoctypeToken (true));
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` in DOCTYPE");
				_state = BeforeDoctypeName;
				_state ();
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#before-doctype-name-state
		/// </summary>
		void BeforeDoctypeName ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Ignore char
				break;
			case 0:
				ParseError ("Unexpected NULL before DOCTYPE name.");
				_state = DoctypeName;
				_currentDoctypeToken = new DoctypeToken (false);
				_currentDoctypeToken.AppendName ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF before DOCTYPE name.");
				_currentDoctypeToken = new DoctypeToken (true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			case '>':
				ParseError ("Unexpected `>` before DOCTYPE name.");
				_currentDoctypeToken = new DoctypeToken (true);
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			default:
				_currentDoctypeToken = new DoctypeToken (false);
				_currentDoctypeToken.AppendName (ch);
				_state = DoctypeName;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-name-state
		/// </summary>
		void DoctypeName ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = AfterDoctypeName;
				break;
			case '>':
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case 0:
				ParseError ("Unexpected NULL in DOCTYPE name.");
				_currentDoctypeToken.AppendName ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF in DOCTYPE name.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			default:
				if ('A' <= ch && ch <= 'Z') {
					ch = char.ToLowerInvariant ((char)ch);
				}
				_currentDoctypeToken.AppendName (ch);
				break;
			}
		}

		static int[] _public = new int[] {
			'P', 'p', 'U', 'u', 'B', 'b', 'L', 'l', 'I', 'i', 'C', 'c'
		};


		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#after-doctype-name-state
		/// </summary>
		void AfterDoctypeName ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Ignore
				break;
			case '>':
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case -1:
				ParseError ("Unexpected EOF after DOCTYPE name.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			case 'P':
			case 'p':
				if (MatchI (_public)) {
					_state = AfterDoctypePublicKeyword;
				}
				else {
					ParseError ("Bad PUBLIC declaration.");
					_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
					_state = BogusDoctype;
				}
				break;
			case 'S':
			case 's':
				throw new NotSupportedException ("SYSTEM");
			default:
				ParseError ("Unexpected `" + (char)ch + "` after DOCTYPE name");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = BogusDoctype;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#after-doctype-public-keyword-state
		/// </summary>
		void AfterDoctypePublicKeyword ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BeforeDoctypePublicIdentifier;
				break;
			case '\"':
				ParseError ("Unexpected `\"` after PUBLIC keyword.");
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypePublicIdentifierDoubleQuoted;
				break;
			case '\'':
				ParseError ("Unexpected `\'` after PUBLIC keyword.");
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypePublicIdentifierSingleQuoted;
				break;
			case '>':
				ParseError ("Unexpected `>` after PUBLIC keyword.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case -1:
				ParseError ("Unexpected EOF after PUBLIC keyword.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` after PUBLIC keyword.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = BogusDoctype;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-public-identifier-double-quoted-state
		/// </summary>
		void DoctypePublicIdentifierDoubleQuoted ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\"':
				_state = AfterDoctypePublicIdentifier;
				break;
			case 0:
				ParseError ("Unexpected NULL in PUBLIC identifier.");
				_currentDoctypeToken.AppendPublicIdentifier (ch);
				break;
			case '>':
				ParseError ("Unexpected `>` in PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case -1:
				ParseError ("Unexpected EOF in PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			default:
				_currentDoctypeToken.AppendPublicIdentifier (ch);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#after-doctype-public-identifier-state
		/// </summary>
		void AfterDoctypePublicIdentifier ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				_state = BetweenDoctypePublicAndSystemIdentifiers;
				break;
			case '>':
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case '\"':
				ParseError ("Unexpected `\"` after PUBLIC identifier.");
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypeSystemIdentifierDoubleQuoted;
				break;
			case '\'':
				ParseError ("Unexpected `\'` after PUBLIC identifier.");
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypeSystemIdentifierSingleQuoted;
				break;
			case -1:
				ParseError ("Unexpected EOF after PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` after PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = BogusDoctype;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-system-identifier-double-quoted-state
		/// </summary>
		void DoctypeSystemIdentifierDoubleQuoted ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-system-identifier-single-quoted-state
		/// </summary>
		void DoctypeSystemIdentifierSingleQuoted ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#between-doctype-public-and-system-identifiers-state
		/// </summary>
		void BetweenDoctypePublicAndSystemIdentifiers ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#doctype-public-identifier-single-quoted-state
		/// </summary>
		void DoctypePublicIdentifierSingleQuoted ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#before-doctype-public-identifier-state
		/// </summary>
		void BeforeDoctypePublicIdentifier ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				// Ignore char
				break;
			case '\"':
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypePublicIdentifierDoubleQuoted;
				break;
			case '\'':
				_currentDoctypeToken.SetEmptyPublicIdentifier ();
				_state = DoctypePublicIdentifierSingleQuoted;
				break;
			case '>':
				ParseError ("Unexpected `>` before PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			case -1:
				ParseError ("Unexpected EOF before PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			default:
				ParseError ("Unexpected `" + (char)ch + "` before PUBLIC identifier.");
				_currentDoctypeToken.SetFlag (TokenFlags.ForceQuirks, true);
				_state = BogusDoctype;
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#bogus-doctype-state
		/// </summary>
		void BogusDoctype ()
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/Overview.html#bogus-comment-state
		/// </summary>
		void BogusComment ()
		{
			throw new NotImplementedException ();
		}
		
		#endregion
	}
	

}


