using UnityEngine;
using System.Collections;

namespace Pathfinding.Util {
	
	/** Simple implementation of a GUID */
	public struct Guid {
	
		//private byte b0,b1,b2,b3,b4,b5,b6,b7,b8,b9,b10,b11,b12,b13,b14,b15;
		
		const string hex = "0123456789ABCDEF";
		
		public static readonly Guid zero = new Guid(new byte[16]);
		public static readonly string zeroString = new Guid(new byte[16]).ToString();

		private ulong _a, _b;
		
		public Guid (byte[] bytes) {
			_a =((ulong)bytes[0] << 8*0) | 
				((ulong)bytes[1] << 8*1) | 
				((ulong)bytes[2] << 8*2) | 
				((ulong)bytes[3] << 8*3) | 
				((ulong)bytes[4] << 8*4) | 
				((ulong)bytes[5] << 8*5) | 
				((ulong)bytes[6] << 8*6) | 
				((ulong)bytes[7] << 8*7);
			
			_b =((ulong)bytes[8] <<  8*0) |
				((ulong)bytes[9] <<  8*1) |
				((ulong)bytes[10] << 8*2) |
				((ulong)bytes[11] << 8*3) | 
				((ulong)bytes[12] << 8*4) | 
				((ulong)bytes[13] << 8*5) | 
				((ulong)bytes[14] << 8*6) | 
				((ulong)bytes[15] << 8*7);
			
			/*b1 = bytes[1];
			b2 = bytes[2];
			b3 = bytes[3];
			b4 = bytes[4];
			b5 = bytes[5];
			b6 = bytes[6];
			b7 = bytes[7];
			b8 = bytes[8];
			b9 = bytes[9];
			b10 = bytes[10];
			b11 = bytes[11];
			b12 = bytes[12];
			b13 = bytes[13];
			b14 = bytes[14];
			b15 = bytes[15];*/
		}
		
		public Guid (string str) {
			/*b0 = 0;
			b1 = 0;
			b2 = 0;
			b3 = 0;
			b4 = 0;
			b5 = 0;
			b6 = 0;
			b7 = 0;
			b8 = 0;
			b9 = 0;
			b10 = 0;
			b11 = 0;
			b12 = 0;
			b13 = 0;
			b14 = 0;
			b15 = 0;*/
			
			_a = 0;
			_b = 0;
			
			if (str.Length < 32) 
				throw new System.FormatException ("Invalid Guid format");
			
			int counter = 0;
			int i = 0;
			int offset = 15*4;
			
			for (;counter < 16;i++) {
				if (i >= str.Length)
					throw new System.FormatException ("Invalid Guid format. String too short");
				
				char c = str[i];
				if (c == '-') continue;
				
				//Neat trick, perhaps a bit slow, but one will probably not use Guid parsing that much
				int value = hex.IndexOf(char.ToUpperInvariant(c));
				if (value == -1)
					throw new System.FormatException ("Invalid Guid format : "+c+" is not a hexadecimal character");
				
				_a |= (ulong)value << offset;
				//SetByte (counter,(byte)value);
				offset -= 4;
				counter++;
			}
			
			offset = 15*4;
			for (;counter < 32;i++) {
				if (i >= str.Length)
					throw new System.FormatException ("Invalid Guid format. String too short");
				
				char c = str[i];
				if (c == '-') continue;
				
				//Neat trick, perhaps a bit slow, but one will probably not use Guid parsing that much
				int value = hex.IndexOf(char.ToUpperInvariant(c));
				if (value == -1)
					throw new System.FormatException ("Invalid Guid format : "+c+" is not a hexadecimal character");
				
				_b |= (ulong)value << offset;
				//SetByte (counter,(byte)value);
				offset -= 4;
				counter++;
			}
		}
		
		public static Guid Parse (string input) {
			return new Guid(input);
		}
		
		public byte[] ToByteArray () {
			byte[] bytes = new byte[16];
			byte[] ba = System.BitConverter.GetBytes(_a);
			byte[] bb = System.BitConverter.GetBytes(_b);
			
			for (int i=0;i<8;i++) {
				bytes[i] = ba[i];
				bytes[i+8] = bb[i];
			
			}
			return bytes;
		}			
		
		private static System.Random random = new System.Random();
		
		public static Guid NewGuid () {
			byte[] bytes = new byte[16];
			random.NextBytes(bytes);
			return new Guid(bytes);
		}
		
		/*private void SetByte (int i, byte value) {
			switch (i) {
				case 0: b0 = value; break;
				case 1: b1 = value; break;
				case 2: b2 = value; break;
				case 3: b3 = value; break;
				case 4: b4 = value; break;
				case 5: b5 = value; break;
				case 6: b6 = value; break;
				case 7: b7 = value; break;
				case 8: b8 = value; break;
				case 9: b9 = value; break;
				case 10: b10 = value; break;
				case 11: b11 = value; break;
				case 12: b12 = value; break;
				case 13: b13 = value; break;
				case 14: b14 = value; break;
				case 15: b15 = value; break;
				default: throw new System.IndexOutOfRangeException ("Cannot set byte value "+i+", only 0...15 permitted");
			}
		}*/
		
		public static bool operator == (Guid lhs, Guid rhs) {
			return lhs._a == rhs._a && lhs._b == rhs._b;
		}
		
		public static bool operator != (Guid lhs, Guid rhs) {
			return lhs._a != rhs._a || lhs._b != rhs._b;
		}
		
		public override bool Equals (System.Object _rhs) {
			if (!(_rhs is Guid)) return false;
			
			Guid rhs = (Guid)_rhs;
			
			return this._a == rhs._a && this._b == rhs._b;
		}
		
		public override int GetHashCode () {
			ulong ab = _a ^ _b;
			return (int)(ab >> 32) ^ (int)ab;
		}
		
		/*public static bool operator == (Guid lhs, Guid rhs) {
			return
					lhs.b0 == rhs.b0 &&
					lhs.b1 == rhs.b1 &&
					lhs.b2 == rhs.b2 &&
					lhs.b3 == rhs.b3 &&
					lhs.b4 == rhs.b4 &&
					lhs.b5 == rhs.b5 &&
					lhs.b6 == rhs.b6 &&
					lhs.b7 == rhs.b7 &&
					lhs.b8 == rhs.b8 &&
					lhs.b9 == rhs.b9 &&
					lhs.b10 == rhs.b10 &&
					lhs.b11 == rhs.b11 &&
					lhs.b12 == rhs.b12 &&
					lhs.b13 == rhs.b13 &&
					lhs.b14 == rhs.b14 &&
					lhs.b15 == rhs.b15;
		}
		
		public static bool operator != (Guid lhs, Guid rhs) {
			return
					lhs.b0 != rhs.b0 ||
					lhs.b1 != rhs.b1  ||
					lhs.b2 != rhs.b2  ||
					lhs.b3 != rhs.b3  ||
					lhs.b4 != rhs.b4  ||
					lhs.b5 != rhs.b5  ||
					lhs.b6 != rhs.b6  ||
					lhs.b7 != rhs.b7  ||
					lhs.b8 != rhs.b8  ||
					lhs.b9 != rhs.b9  ||
					lhs.b10 != rhs.b10  ||
					lhs.b11 != rhs.b11  ||
					lhs.b12 != rhs.b12  ||
					lhs.b13 != rhs.b13  ||
					lhs.b14 != rhs.b14  ||
					lhs.b15 != rhs.b15;
		}*/
		
		private static System.Text.StringBuilder text;
		
		public override string ToString () {
			if (text == null) {
				text = new System.Text.StringBuilder();
			}
			lock (text) {
				text.Length = 0;
				text.Append (_a.ToString("x16")).Append('-').Append(_b.ToString("x16"));
				return text.ToString();
			}
		}
	}
}