#pragma warning disable 0162
#pragma warning disable 0414
#define PROFILE
using System;

namespace Pathfinding
{
	
	
	class Profile {
		const bool PROFILE_MEM = false;
		
		public string name;
		System.Diagnostics.Stopwatch w;
		int counter = 0;
		long mem = 0;
		long smem = 0;
		
		int control = 1 << 30;
		bool dontCountFirst = false;
		
		public int ControlValue () {
			return control;
		}
		
		public Profile (string name) {
			this.name = name;
			w = new System.Diagnostics.Stopwatch();
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		public void Start () {
			if (PROFILE_MEM) {
				smem = System.GC.GetTotalMemory(false);
			}
			if (dontCountFirst && counter == 1) return;
			w.Start();
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		public void Stop () {
			counter++;
			if (dontCountFirst && counter == 1) return;
			
			w.Stop();
			if (PROFILE_MEM) {
				mem += System.GC.GetTotalMemory(false)-smem;
			}
			
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		/** Log using Debug.Log */
		public void Log () {
			UnityEngine.Debug.Log (ToString());
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		/** Log using System.Console */
		public void ConsoleLog () {
			System.Console.WriteLine (ToString());
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		public void Stop (int control) {
			counter++;
			if (dontCountFirst && counter == 1) return;
			
			w.Stop();
			if (PROFILE_MEM) {
				mem += System.GC.GetTotalMemory(false)-smem;
			}
			
			if (this.control == 1 << 30) this.control = control;
			else if (this.control != control) throw new System.Exception("Control numbers do not match " + this.control + " != " + control);
		}
		
		[System.Diagnostics.ConditionalAttribute("PROFILE")]
		public void Control (Profile other) {
			if (ControlValue() != other.ControlValue()) {
				throw new System.Exception("Control numbers do not match ("+name + " " + other.name + ") " + this.ControlValue() + " != " + other.ControlValue());
			}
		}
		
		public override string ToString () {
			string s = name + " #" + counter + " " + w.Elapsed.TotalMilliseconds.ToString("0.0 ms") + " avg: " + (w.Elapsed.TotalMilliseconds/counter).ToString("0.00 ms");
			if (PROFILE_MEM) {
				s += " avg mem: " + (mem/(1.0*counter)).ToString("0 bytes");
			}
			return s;
		}

	}
}

