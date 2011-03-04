using System;
using System.IO;

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
		
		void Emit ()
		{
			Console.WriteLine (_lastToken);
		}
		
		void ParseError (string message)
		{
			Console.WriteLine ("! " + message);
		}
		
		#region Tokens
		
		Token _lastToken = new Token ();
		
		void CharacterToken (int ch)
		{
			_lastToken.Type = TokenType.Character;
			_lastToken.Character = (char)ch;
		}
		
		void EndOfFileToken ()
		{
			_lastToken.Type = TokenType.EndOfFile;
		}
		
		void StartTagToken (int ch)
		{
			_lastToken.Type = TokenType.StartTag;
			_lastToken.TagName = char.ToLowerInvariant((char)ch).ToString ();
		}
		
		#endregion
		
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
				CharacterToken (_currentInputChar);
				Emit ();
				break;
			case -1:
				EndOfFileToken ();
				Emit ();
				break;
			default:
				CharacterToken (_currentInputChar);
				Emit ();
				break;
			}
		}
		
		void CharacterReferenceInData ()
		{
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
				StartTagToken (ch);
				_state = TagName;
			}
			else if (ch == '?') {
				ParseError ("Bogus comment.");
				_state = BogusComment;
			}
			else {
				ParseError ("Expected tag name.");
				CharacterToken ('<');
				Emit ();
				_state = Data;
				_state ();
			}
		}
		
		void TagName ()
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
	
	public class Token
	{
		public TokenType Type;
		
		public char Character;
		public string TagName;
		
		public override string ToString ()
		{
			return string.Format ("[Token Type={0}]", Type);
		}
	}
}



