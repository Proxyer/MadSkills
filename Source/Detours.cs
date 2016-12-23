﻿using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using Verse;
using RimWorld;

namespace RTMadSkills
{
	internal static class Detours_RTMadSkills
	{
		internal static void Interval(this SkillRecord skillRecord)
		{
			if (Find.TickManager.TicksAbs % 60000 <= 200)
			{
				skillRecord.xpSinceMidnight = 0f;
			}
			if (skillRecord.XpProgressPercent < 0.01f)
			{
				return;
			}
			else
			{
				float xp = 0.0f;
				switch (skillRecord.Level)
				{
					case 10: xp = -0.1f; break;
					case 11: xp = -0.2f; break;
					case 12: xp = -0.4f; break;
					case 13: xp = -0.65f; break;
					case 14: xp = -1.0f; break;
					case 15: xp = -1.5f; break;
					case 16: xp = -2.0f; break;
					case 17: xp = -3.0f; break;
					case 18: xp = -4.0f; break;
					case 19: xp = -6.0f; break;
					case 20: xp = -8.0f; break;
				}
				skillRecord.Learn(xp, false);
			}
		}
	}

	[StaticConstructorOnStartup]
	internal static class DetourInjector
	{
		static DetourInjector()
		{
			LongEventHandler.QueueLongEvent(Inject, "LibraryStartup", false, null);
		}

		public static void Inject()
		{
			MethodInfo originalInterval = typeof(SkillRecord).GetMethod(
				"Interval",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new Type[] { },
				null);

			MethodInfo modifiedInterval = typeof(Detours_RTMadSkills).GetMethod(
				"Interval",
				BindingFlags.Static | BindingFlags.NonPublic,
				null, 
				new Type[] { typeof(SkillRecord) },
				null);

			if (Detours.TryDetourFromTo(originalInterval, modifiedInterval))
			{
				Log.Message("Mad Skills: detour succesful.");
			}
			else
			{
				Log.Error("Mad Skills: detour failed!");
			}
		}
	}

	/// <summary>
	/// As seen in Combat Realism.
	/// </summary>
	public static class Detours
	{

		private static List<string> detoured = new List<string>();
		private static List<string> destinations = new List<string>();

		/**
            This is a basic first implementation of the IL method 'hooks' (detours) made possible by RawCode's work;
            https://ludeon.com/forums/index.php?topic=17143.0

            Performs detours, spits out basic logs and warns if a method is detoured multiple times.
        **/
		public static unsafe bool TryDetourFromTo(MethodInfo source, MethodInfo destination)
		{
			// error out on null arguments
			if (source == null)
			{
				Log.Error("Source MethodInfo is null: Detours");
				return false;
			}

			if (destination == null)
			{
				Log.Error("Destination MethodInfo is null: Detours");
				return false;
			}

			// keep track of detours and spit out some messaging
			string sourceString = source.DeclaringType.FullName + "." + source.Name + " @ 0x" + source.MethodHandle.GetFunctionPointer().ToString("X" + (IntPtr.Size * 2).ToString());
			string destinationString = destination.DeclaringType.FullName + "." + destination.Name + " @ 0x" + destination.MethodHandle.GetFunctionPointer().ToString("X" + (IntPtr.Size * 2).ToString());

			detoured.Add(sourceString);
			destinations.Add(destinationString);

			if (IntPtr.Size == sizeof(Int64))
			{
				// 64-bit systems use 64-bit absolute address and jumps
				// 12 byte destructive

				// Get function pointers
				long Source_Base = source.MethodHandle.GetFunctionPointer().ToInt64();
				long Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt64();

				// Native source address
				byte* Pointer_Raw_Source = (byte*)Source_Base;

				// Pointer to insert jump address into native code
				long* Pointer_Raw_Address = (long*)(Pointer_Raw_Source + 0x02);

				// Insert 64-bit absolute jump into native code (address in rax)
				// mov rax, immediate64
				// jmp [rax]
				*(Pointer_Raw_Source + 0x00) = 0x48;
				*(Pointer_Raw_Source + 0x01) = 0xB8;
				*Pointer_Raw_Address = Destination_Base; // ( Pointer_Raw_Source + 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 )
				*(Pointer_Raw_Source + 0x0A) = 0xFF;
				*(Pointer_Raw_Source + 0x0B) = 0xE0;

			}
			else
			{
				// 32-bit systems use 32-bit relative offset and jump
				// 5 byte destructive

				// Get function pointers
				int Source_Base = source.MethodHandle.GetFunctionPointer().ToInt32();
				int Destination_Base = destination.MethodHandle.GetFunctionPointer().ToInt32();

				// Native source address
				byte* Pointer_Raw_Source = (byte*)Source_Base;

				// Pointer to insert jump address into native code
				int* Pointer_Raw_Address = (int*)(Pointer_Raw_Source + 1);

				// Jump offset (less instruction size)
				int offset = (Destination_Base - Source_Base) - 5;

				// Insert 32-bit relative jump into native code
				*Pointer_Raw_Source = 0xE9;
				*Pointer_Raw_Address = offset;
			}

			// done!
			return true;
		}

	}
}