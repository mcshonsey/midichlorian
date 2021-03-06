﻿using System.Collections.Generic;
using System.Linq;
using Midi;

namespace YuriyGuts.Midichlorian.VSPackage
{
    /// <summary>
    /// Recognizes MidiInputTriggers in events received from a MIDI device.
    /// </summary>
    internal class MidiMappingMatcher
    {
        private readonly MidiMappingProfile mappingProfile;
        private readonly List<MidiMappingRecord> chordMappingsOrdered; 

        public MidiMappingMatcher(MidiMappingProfile mappingProfile)
        {
            this.mappingProfile = mappingProfile;
            
            // Cache chords aggressively, we'll need to scan them quickly.
            chordMappingsOrdered = mappingProfile.Mappings
                .Where(m => m.Trigger.IsChord)
                .OrderByDescending(m => m.Trigger.Pitches.Count())
                .ToList();
        }

        public MidiMappingRecord[] FindSingleNoteMatches(Pitch pitch)
        {
            return mappingProfile.Mappings
                .Where(mapping => mapping.Trigger.IsSingleNote && mapping.Trigger.Pitches[0] == pitch)
                .ToArray();
        }

        public MidiMappingRecord[] FindChordMatchesContainingNote(Pitch pitch)
        {
            return mappingProfile.Mappings
                .Where(mapping => mapping.Trigger.IsChord && mapping.Trigger.Pitches.Any(p => p == pitch))
                .ToArray();
        }

        public MidiMappingRecord[] FindMatchesInBuffer(Pitch[] buffer)
        {
            var results = new List<MidiMappingRecord>();
            var events = buffer.Cast<Pitch?>().ToArray();

            // Process chords first. Try to capture a chord with as many notes as possible.
            // This is O(scary), but seems quick enough in practice. (c) StackOverflow #184618
            foreach (var mapping in chordMappingsOrdered)
            {
                bool isChordComplete = mapping.Trigger.Pitches.All(p => events.Any(e => e.HasValue && e == p));
                if (isChordComplete)
                {
                    // Exclude matched notes from further processing.
                    for (int i = 0; i < events.Length; i++)
                    {
                        var pitch = events[i].Value;
                        if (mapping.Trigger.Pitches.Any(p => p == pitch))
                        {
                            events[i] = null;
                        }
                    }
                    results.Add(mapping);
                }
            }

            // Process remaining single notes.
            foreach (var e in events.Where(e => e.HasValue))
            {
                results.AddRange(FindSingleNoteMatches(e.Value));
            }

            return results.ToArray();
        }
    }
}
