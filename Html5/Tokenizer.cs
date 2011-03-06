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

		Token _currentTag = null;
		Token _currentDoctypeToken = null;
		Token _currentComment = null;

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
			Console.WriteLine (token);
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

		void CharacterReferenceInData ()
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
				_currentTag = Token.StartTagToken (ch);
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
			throw new NotImplementedException ();
		}

		void SelfClosingStartTag ()
		{
			throw new NotImplementedException ();
		}
			
		void EndTagOpen ()
		{
			throw new NotImplementedException ();
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
					_currentComment = Token.Comment ();
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
				Emit (Token.Doctype (true));
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
				_currentDoctypeToken = Token.Doctype (false);
				_currentDoctypeToken.AppendName ('\uFFFD');
				break;
			case -1:
				ParseError ("Unexpected EOF before DOCTYPE name.");
				_currentDoctypeToken = Token.Doctype (true);
				Emit (_currentDoctypeToken);
				_state = Data;
				_state ();
				break;
			case '>':
				ParseError ("Unexpected `>` before DOCTYPE name.");
				_currentDoctypeToken = Token.Doctype (true);
				_state = Data;
				Emit (_currentDoctypeToken);
				break;
			default:
				_currentDoctypeToken = Token.Doctype (false);
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
	
	public enum TokenType
	{
		Character,
		EndOfFile,
		StartTag,
		Doctype,
		Comment
	}

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

	public enum TokenFlags
	{
		None = 0,
		ForceQuirks = 1
	}

	public class Token
	{
		public TokenType Type;

		TokenFlags _flags;

		public void SetFlag (TokenFlags flag, bool on) {
			if (on) { _flags |= flag; }
			else { _flags &= ~flag; }
		}
		
		public char Character;

		StringBuilder _publicIdentifier;

		public void SetEmptyPublicIdentifier ()
		{
			_publicIdentifier = new StringBuilder ();
		}

		public void AppendPublicIdentifier (int ch)
		{
			if (_publicIdentifier == null) {
				_publicIdentifier = new StringBuilder();
			}
			_publicIdentifier.Append ((char)ch);
		}

		StringBuilder _data;

		public void AppendData (int ch)
		{
			if (_data == null) {
				_data = new StringBuilder();
			}
			_data.Append ((char)ch);
		}

		StringBuilder _name;

		public void AppendName (int ch)
		{
			if (_name != null) {
				if ('A' <= ch && ch <= 'Z') {
					ch -= 0x20;
				}
				_name.Append ((char)ch);
			}
		}

		List<Attribute> _attributes;

		public void AddAttribute (Attribute attr)
		{
			if (_attributes == null) {
				_attributes = new List<Attribute>();
			}
			_attributes.Add (attr);
		}

		public Attribute CurrentAttribute {
			get { return (_attributes == null) ? null :
					_attributes[_attributes.Count - 1]; }
		}
		
		public override string ToString ()
		{
			return string.Format ("[Token Type={0}]", Type);
		}

		public static Token Comment ()
		{
			return new Token () {
				Type = TokenType.Comment,
			};
		}

		public static Token Doctype (bool forceQuirksFlag)
		{
			return new Token () {
				Type = TokenType.Doctype,
				_name = new StringBuilder()
			};
		}

		public static Token CharacterToken (int ch)
		{
			return new Token () {
				Type = TokenType.Character,
				Character = (char)ch
			};
		}

		public static Token EndOfFileToken ()
		{
			return new Token () {
				Type = TokenType.EndOfFile
			};
		}

		public static Token StartTagToken (int ch)
		{
			var tok = new Token () {
				Type = TokenType.StartTag,
				_name = new StringBuilder()
			};
			tok.AppendName (ch);
			return tok;
		}
	}
}


