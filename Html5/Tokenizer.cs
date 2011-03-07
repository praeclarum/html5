using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Html5
{
	public interface ITokenObserver
	{
		void OnNext (Token token);
	}

	public class Tokenizer
	{
		TextReader _reader;
		
		Action _state;
		
		int _currentInputChar;

		TagToken _currentTag = null;
		StartTagToken _lastStartTag = null;
		DoctypeToken _currentDoctypeToken = null;
		CommentToken _currentComment = null;

		int _additionalAllowedChar = -2;

		bool _isEof = false;

		public Tokenizer (TextReader reader)
		{
			if (reader == null) throw new ArgumentNullException ("reader");
			_reader = reader;
			_state = Data;
		}

		public void SetRcdataState ()
		{
			_state = Rcdata;
		}

		public void SetScriptDataState ()
		{
			_state = ScriptData;
		}

		public void SetRawTextState ()
		{
			_state = RawText;
		}

		public void Run ()
		{
			for (;;) {
				var ch = ConsumeNextInputChar ();
				_state ();

				if (ch == -1) {
					return;
				}
			}
		}

		ITokenObserver _observer;

		public void Subscribe (ITokenObserver obs)
		{
			_observer = obs;
		}

		void Emit (Token token)
		{
			if (token.Type == TokenType.EndOfFile) {
				_isEof = true;
			}
			if (_observer != null) {
				_observer.OnNext (token);
			}
			Console.Write (token);
		}

		void EmitChar (int ch)
		{
			Emit (Token.CharacterTokenF (ch));
		}

		void ParseError (string message)
		{
			Console.Error.WriteLine ("! " + message);
		}

		int[] _charIndex = new int [32];

		int _writeCharIndex = 0;
		int _readCharIndex = 0;
		
		int ConsumeNextInputChar ()
		{
			if (_readCharIndex == _writeCharIndex) {
				_charIndex [_readCharIndex] = _reader.Read ();
				_writeCharIndex = (_writeCharIndex + 1) % _charIndex.Length;
			}
			_currentInputChar = _charIndex [_readCharIndex];
			_readCharIndex = (_readCharIndex + 1) % _charIndex.Length;
			return _currentInputChar;
		}

		void UnconsumeInputChar ()
		{
			_readCharIndex -= 1;
			if (_readCharIndex < 0) {
				_readCharIndex += _charIndex.Length;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#consume-a-character-reference
		/// </summary>
		CharacterReferences.CharRef ConsumeCharacterReferenceF ()
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
				return null;
			case '#':
				ch = ConsumeNextInputChar ();
				if (ch == 'x' || ch == 'X') {
					return ConsumeHexCharacterReference ();
				}
				else {
					return ConsumeDecCharacterReference ();
				}
			default:
				if (_additionalAllowedChar > 0 && ch == _additionalAllowedChar) {
					// Not a character reference. No characters are consumed, and nothing is returned. (This is not an error, either.)
					return null;
				}
				else {
					if (CharacterReferences.FirstCharMatches (ch)) {
						var ret = ConsumeNamedCharacterReference ();
						if (!ret.Name.EndsWith(";")) {
							ParseError ("Character reference `" + ret.Name + "` should end with a semicolon.");
						}
						return ret;
					}
					else {
						// If no match can be made, then no characters are consumed, and nothing is returned.
						return null;
					}
				}
			}
		}

		CharacterReferences.CharRef ConsumeNamedCharacterReference ()
		{
			Func<CharacterReferences.CharRef, string> s = x => x.Name;
			var r = ((char)_currentInputChar).ToString ();


			var firstMatches = FindPrefixes (CharacterReferences.All, s, r).ToList ();

			var matches = firstMatches;

			while (matches.Count > 0) {
				if (matches.Count == 1) {
					return matches [0];
				}
				else {
					//
					// Are there any longer matches?
					//
					var ch = ConsumeNextInputChar ();
					var newR = r + (char)ch;
					var longerMatches = FindPrefixes (matches, s, newR).ToList ();

					if (longerMatches.Count == 0) {
						//
						// Rollback looking for an exact match
						//
						UnconsumeInputChar ();

						while (r.Length > 0) {
							foreach (var m in firstMatches) {
								if (s(m) == r) {
									return m;
								}
							}

							r = r.Substring (0, r.Length - 1);
							UnconsumeInputChar ();
						}
						throw new NotImplementedException ("MATCH");
					}
					else {
						matches = longerMatches;
						r = newR;
					}
				}
			}

			throw new NotImplementedException ();
		}

		static IEnumerable<T> FindPrefixes<T> (IEnumerable<T> items, Func<T,string> selector, string prefix)
		{
			foreach (var i in items) {
				var s = selector (i);
				if (s.StartsWith (prefix)) {
					yield return i;
					}
				}
		}

		CharacterReferences.CharRef ConsumeDecCharacterReference ()
		{
			throw new NotImplementedException ();
		}

		CharacterReferences.CharRef ConsumeHexCharacterReference ()
		{
			throw new NotImplementedException ();
		}

		#region Parse States

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#data-state
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
				EmitChar (_currentInputChar);
				break;
			case -1:
				Emit (Token.EndOfFileToken ());
				break;
			default:
				EmitChar (_currentInputChar);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#script-data-state
		/// </summary>
		void ScriptData ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '<':
				_state = ScriptDataLessThanSign;
				break;
			case 0:
				ParseError ("Unexpected NULL in script data.");
				EmitChar ('\uFFFD');
				break;
			case -1:
				Emit (Token.EndOfFileToken ());
				break;
			default:
				EmitChar (_currentInputChar);
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#script-data-less-than-sign-state
		/// </summary>
		void ScriptDataLessThanSign ()
		{
			switch (_currentInputChar)
			{
			case '/':
				_temporaryBuffer = "";
				_state = ScriptDataEndTagOpen;
				break;
			case '!':
				_state = ScriptDataEscapeStart;
				EmitChar ('<');
				EmitChar ('!');
				break;
			default:
				EmitChar ('<');
				_state = ScriptData;
				_state ();
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#script-data-escape-start-state
		/// </summary>
		void ScriptDataEscapeStart ()
		{
			throw new NotImplementedException ();
		}

		void ScriptDataEndTagOpen ()
		{
			var ch = _currentInputChar;

			if (('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z')) {
				_currentTag = new EndTagToken (ch);
				_temporaryBuffer += (char)ch;
				_state = ScriptDataEndTagName;
			}
			else {
				EmitChar ('<');
				EmitChar ('/');
				_state = ScriptData;
				_state ();
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#script-data-end-tag-name-state
		/// </summary>
		void ScriptDataEndTagName ()
		{
			var handled = false;
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				if (IsEndTagAppropriate ()) {
					_state = BeforeAttributeName;
					handled = true;
				}
				break;
			case '/':
				if (IsEndTagAppropriate ()) {
					_state = SelfClosingStartTag;
					handled = true;
				}
				break;
			case '>':
				if (IsEndTagAppropriate ()) {
					Emit (_currentTag);
					_state = Data;
					handled = true;
				}
				break;
			default:
				if (('a' <= ch && ch <= 'z') ||
					('A' <= ch && ch <= 'Z')) {
					_currentTag.AppendName (ch);
					_temporaryBuffer += (char)ch;
					handled = true;
				}
				break;
			}

			if (!handled) {
				EmitChar ('<');
				EmitChar ('/');
				foreach (var c in _temporaryBuffer) {
					EmitChar (c);
				}
				_state = ScriptData;
				_state ();
			}
		}

		void RawText ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '<':
				_state = RawTextLessThanSign;
				break;
			case 0:
				ParseError ("Unexpected NULL in RAWTEXT.");
				EmitChar ('\uFFFD');
				break;
			case -1:
				Emit (Token.EndOfFileToken ());
				break;
			default:
				EmitChar (_currentInputChar);
				break;
			}
		}

		void RawTextLessThanSign ()
		{
			switch (_currentInputChar)
			{
			case '/':
				_temporaryBuffer = "";
				_state = RawTextEndTagOpen;
				break;
			default:
				EmitChar ('<');
				_state = RawText;
				_state ();
				break;
			}
		}

		void RawTextEndTagOpen ()
		{
			var ch = _currentInputChar;

			if (('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z')) {
				_currentTag = new EndTagToken (ch);
				_temporaryBuffer += (char)ch;
				_state = RawTextEndTagName;
			}
			else {
				EmitChar ('<');
				EmitChar ('/');
				_state = RawText;
				_state ();
			}
		}

		void RawTextEndTagName ()
		{
			var handled = false;
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				if (IsEndTagAppropriate ()) {
					_state = BeforeAttributeName;
					handled = true;
				}
				break;
			case '/':
				if (IsEndTagAppropriate ()) {
					_state = SelfClosingStartTag;
					handled = true;
				}
				break;
			case '>':
				if (IsEndTagAppropriate ()) {
					Emit (_currentTag);
					_state = Data;
					handled = true;
				}
				break;
			default:
				if (('a' <= ch && ch <= 'z') ||
					('A' <= ch && ch <= 'Z')) {
					_currentTag.AppendName (ch);
					_temporaryBuffer += (char)ch;
					handled = true;
				}
				break;
			}

			if (!handled) {
				EmitChar ('<');
				EmitChar ('/');
				foreach (var c in _temporaryBuffer) {
					EmitChar (c);
				}
				_state = RawText;
				_state ();
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#rcdata-state
		/// </summary>
		void Rcdata ()
		{
			var ch = _currentInputChar;

			switch (ch)
			{
			case '&':
				_state = CharacterReferenceInRcdata;
				break;
			case '<':
				_state = RcdataLessThanSign;
				break;
			case 0:
				ParseError ("Unexpected NULL in RCDATA.");
				EmitChar ('\uFFFD');
				break;
			case -1:
				Emit (Token.EndOfFileToken ());
				break;
			default:
				EmitChar (_currentInputChar);
				break;
			}
		}

		string _temporaryBuffer = "";

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#rcdata-less-than-sign-state
		/// </summary>
		void RcdataLessThanSign ()
		{
			switch (_currentInputChar)
			{
			case '/':
				_temporaryBuffer = "";
				_state = RcdataEndTagOpen;
				break;
			default:
				EmitChar ('<');
				_state = Rcdata;
				_state ();
				break;
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#rcdata-end-tag-open-state
		/// </summary>
		void RcdataEndTagOpen ()
		{
			var ch = _currentInputChar;

			if (('a' <= ch && ch <= 'z') || ('A' <= ch && ch <= 'Z')) {
				_currentTag = new EndTagToken (ch);
				_temporaryBuffer += (char)ch;
				_state = RcdataEndTagName;
			}
			else {
				EmitChar ('<');
				EmitChar ('/');
				_state = Rcdata;
				_state ();
			}
		}

		bool IsEndTagAppropriate () {
			var endName = (_currentTag != null) ? _currentTag.Name : "";
			var startName = (_lastStartTag != null) ? _lastStartTag.Name : "";
			return endName == startName;
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#rcdata-end-tag-name-state
		/// </summary>
		void RcdataEndTagName ()
		{
			var handled = false;
			var ch = _currentInputChar;

			switch (ch)
			{
			case '\t':
			case '\r':
			case '\n':
			case ' ':
				if (IsEndTagAppropriate ()) {
					_state = BeforeAttributeName;
					handled = true;
				}
				break;
			case '/':
				if (IsEndTagAppropriate ()) {
					_state = SelfClosingStartTag;
					handled = true;
				}
				break;
			case '>':
				if (IsEndTagAppropriate ()) {
					Emit (_currentTag);
					_state = Data;
					handled = true;
				}
				break;
			default:
				if (('a' <= ch && ch <= 'z') ||
					('A' <= ch && ch <= 'Z')) {
					_currentTag.AppendName (ch);
					_temporaryBuffer += (char)ch;
					handled = true;
				}
				break;
			}

			if (!handled) {
				EmitChar ('<');
				EmitChar ('/');
				foreach (var c in _temporaryBuffer) {
					EmitChar (c);
				}
				_state = Rcdata;
				_state ();
			}
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#character-reference-in-rcdata-state
		/// </summary>
		void CharacterReferenceInRcdata ()
		{
			_additionalAllowedChar = -2;
			var ret = ConsumeCharacterReferenceF ();
			if (ret == null) {
				UnconsumeInputChar ();
				EmitChar ('&');
			}
			else {
				EmitChar (ret.Char1);
				if (ret.Char2 != 0) EmitChar (ret.Char2);
			}

			_state = Rcdata;
		}

		Action _characterReferenceInAttributeValuePrev;

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#character-reference-in-attribute-value-state
		/// </summary>
		void CharacterReferenceInAttributeValue ()
		{
			var ret = ConsumeCharacterReferenceF ();
			if (ret == null) {
				UnconsumeInputChar ();
				EmitChar ('&');
			}
			else {
				EmitChar (ret.Char1);
				if (ret.Char2 != 0) EmitChar (ret.Char2);
			}

			_state = _characterReferenceInAttributeValuePrev;
		}

		/// <summary>
		/// http://dev.w3.org/html5/spec/tokenization.html#character-reference-in-data-state
		/// </summary>
		void CharacterReferenceInData ()
		{
			_additionalAllowedChar = -2;
			var ret = ConsumeCharacterReferenceF ();
			if (ret == null) {
				UnconsumeInputChar ();
				EmitChar ('&');
			}
			else {
				EmitChar (ret.Char1);
				if (ret.Char2 != 0) EmitChar (ret.Char2);
			}

			_state = Data;
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
				_lastStartTag = new StartTagToken (ch);
				_currentTag = _lastStartTag;
				_state = TagName;
			}
			else if (ch == '?') {
				ParseError ("Bogus comment.");
				_state = BogusComment;
			}
			else {
				ParseError ("Expected tag name.");
				EmitChar ('<');
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
				_characterReferenceInAttributeValuePrev = _state;
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
				_characterReferenceInAttributeValuePrev = _state;
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
				_characterReferenceInAttributeValuePrev = _state;
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
				EmitChar ('<');
				EmitChar ('/');
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


