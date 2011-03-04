using System;
using System.IO;
using System.Collections.Generic;

namespace Html5.Tokenizer
{
	public class Tokenizer
	{
		TextReader _reader;
		
		Action _state;
		
		int _currentInputChar;
		
		public Tokenizer (TextReader reader)
		{
			if (reader == null) throw new ArgumentNullException ("reader");
			_reader = reader;
		}
		
		int ConsumeNextInputChar ()
		{
			_currentInputChar = _reader.Read ();
			return _currentInputChar;
		}
		
		void Emit (Token token)
		{
			Console.WriteLine (token);
		}
		
		void ParseError (string message)
		{
			Console.WriteLine ("! " + message);
		}
		
		#region Parse States
		
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
		}

		Token _currentTagToken = null;
		
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
				_currentTagToken = Token.StartTagToken (ch);
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
				Emit (_currentTagToken);
				_currentTagToken = null;
				break;
			case '\u0000':
				ParseError ("Unexpected NULL in tag name.");
				_currentTagToken.TagName += '\uFFFD';
				break;
			case -1:
				ParseError ("Unexpected EOF in tag name.");
				_state = Data;
				_state ();
				break;
			default:
				{
					var ch = (char)_currentInputChar;
					if ('A' <= ch && ch <= 'Z') {
						ch = char.ToLowerInvariant (ch);
					}
					_currentTagToken.TagName += ch;
				}
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
				Emit (_currentTagToken);
				break;
			case '\u0000':
				ParseError ("Unexpected NULL before attribute name.");
				attr = new Attribute ("\uFFFD");
				_currentTagToken.AddAttribute (attr);
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
				_currentTagToken.AddAttribute (attr);
				_state = AttributeName;
				break;
			default:
				attr = new Attribute (_currentInputChar);
				_currentTagToken.AddAttribute (attr);
				_state = AttributeName;
				break;
			}
		}

		void AttributeName ()
		{
		}

		void SelfClosingStartTag ()
		{
		}
			
		void EndTagOpen ()
		{
		}
			
		void MarkupDeclarationOpen ()
		{
		}
		
		void BogusComment ()
		{
		}
		
		#endregion
	}
	
	public enum TokenType
	{
		Character,
		EndOfFile,
		StartTag
	}

	public class Attribute
	{
		public string Name;

		public Attribute (string name) {
			Name = name;
		}

		public Attribute (int ch) {
			Name = ((char)ch).ToString ();
		}
	}
	
	public class Token
	{
		public TokenType Type;
		
		public char Character;
		public string TagName;

		List<Attribute> _attributes;

		public void AddAttribute (Attribute attr)
		{
			if (_attributes == null) {
				_attributes = new List<Attribute>();
			}
			_attributes.Add (attr);
		}
		
		public override string ToString ()
		{
			return string.Format ("[Token Type={0}]", Type);
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
			return new Token () {
				Type = TokenType.StartTag,
				TagName = char.ToLowerInvariant((char)ch).ToString ()
			};
		}
	}
}


