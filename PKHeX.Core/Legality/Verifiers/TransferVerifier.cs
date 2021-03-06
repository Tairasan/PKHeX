﻿using System;
using System.Collections.Generic;
using static PKHeX.Core.LegalityCheckStrings;

namespace PKHeX.Core
{
    /// <summary>
    /// Verifies the transfer data for a <see cref="PKM"/> that has been irreversably transferred forward.
    /// </summary>
    public sealed class TransferVerifier : Verifier
    {
        protected override CheckIdentifier Identifier => CheckIdentifier.Encounter;

        public override void Verify(LegalityAnalysis data)
        {
            throw new Exception("Don't call via this.");
        }

        public void VerifyTransferLegalityG12(LegalityAnalysis data)
        {
            VerifyTransferVCNatureEXP(data);
            VerifyShinyXorIfShiny(data);
        }

        private void VerifyTransferVCNatureEXP(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            var met = pkm.Met_Level;

            if (met == 100) // check for precise match, can't receive EXP after transfer.
            {
                var nature = Experience.GetNatureVC(pkm.EXP);
                if (nature != pkm.Nature)
                    data.AddLine(GetInvalid(LTransferNature));
                return;
            }
            if (met <= 2) // Not enough EXP to have every nature -- check for exclusions!
            {
                var pi = pkm.PersonalInfo;
                var growth = pi.EXPGrowth;
                var nature = pkm.Nature;
                bool valid = VerifyVCNature(growth, nature);
                if (!valid)
                    data.AddLine(GetInvalid(LTransferNature));
            }
        }

        private static bool VerifyVCNature(int growth, int nature)
        {
            // exp % 25 with a limited amount of EXP does not allow for every nature
            return growth switch
            {
                0 => // MediumFast -- Can't be Brave, Adamant, Naughty, Bold, Docile, or Relaxed
                (nature < (int) Nature.Brave || nature > (int) Nature.Relaxed),
                4 => // Fast -- Can't be Gentle, Sassy, Careful, Quirky, Hardy, Lonely, Brave, Adamant, Naughty, or Bold
                (nature < (int) Nature.Gentle && nature > (int) Nature.Bold),
                5 => // Slow -- Can't be Impish or Lax
                (nature != (int) Nature.Impish && nature != (int) Nature.Lax),
                _ => true
            };
        }

        private static void VerifyShinyXorIfShiny(LegalityAnalysis data)
        {
            // Star, not square. Requires transferring a shiny and having the initially random PID to already be a Star shiny.
            // (15:65536, ~1:4096) odds on a given shiny transfer!
            var xor = data.pkm.ShinyXor;
            if (xor < 16 && xor != 0)
                data.AddLine(Get(LEncStaticPIDShiny, ParseSettings.Gen7TransferStarPID, CheckIdentifier.PID));
        }

        public void VerifyTransferLegalityG3(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            if (pkm.Format == 4) // Pal Park (3->4)
            {
                if (pkm.Met_Location != Locations.Transfer3)
                    data.AddLine(GetInvalid(LEggLocationPalPark));
            }
            else // Transporter (4->5)
            {
                if (pkm.Met_Location != Locations.Transfer4)
                    data.AddLine(GetInvalid(LTransferEggLocationTransporter));
            }
        }

        public void VerifyTransferLegalityG4(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            int loc = pkm.Met_Location;
            if (loc == Locations.Transfer4)
                return;

            // Crown met location must be present if transferred via lock capsule
            switch (pkm.Species)
            {
                case (int)Species.Celebi:
                    if (loc != Locations.Transfer4_CelebiUnused && loc != Locations.Transfer4_CelebiUsed)
                        data.AddLine(GetInvalid(LTransferMet));
                    break;
                case (int)Species.Raikou:
                case (int)Species.Entei:
                case (int)Species.Suicune:
                    if (loc != Locations.Transfer4_CrownUnused && loc != Locations.Transfer4_CrownUsed)
                        data.AddLine(GetInvalid(LTransferMet));
                    break;
                default:
                    data.AddLine(GetInvalid(LTransferEggLocationTransporter));
                    break;
            }
        }

        public void VerifyTransferLegalityG8(LegalityAnalysis data)
        {
            var pkm = data.pkm;
            int species = pkm.Species;
            var pi = (PersonalInfoSWSH)PersonalTable.SWSH.GetFormeEntry(species, pkm.AltForm);
            if (!pi.IsPresentInGame) // Can't transfer
            {
                data.AddLine(GetInvalid(LTransferBad));
                return;
            }

            var enc = data.EncounterMatch;
            if (enc.Version == GameVersion.GO || (enc is WC8 wc && wc.IsHOMEGift))
            {
                VerifyHOMETracker(data, pkm);
            }
            else if (enc.Generation < 8 && pkm.Format >= 8)
            {
                if (enc is EncounterStatic7 s && AltFormInfo.IsTotemForm(s.Species, s.Form, 7))
                {
                    if (Legal.Totem_NoTransfer.Contains(s.Species))
                        data.AddLine(GetInvalid(LTransferBad));
                    if (pkm.AltForm != AltFormInfo.GetTotemBaseForm(s.Species, s.Form))
                        data.AddLine(GetInvalid(LTransferBad));
                }

                VerifyHOMETransfer(data, pkm);
                VerifyHOMETracker(data, pkm);
            }
        }

        private void VerifyHOMETransfer(LegalityAnalysis data, PKM pkm)
        {
            if (!(pkm is IScaledSize s))
                return;

            if (pkm.LGPE || pkm.GO)
                return; // can have any size value
            if (s.HeightScalar != 0)
                data.AddLine(GetInvalid(LTransferBad));
            if (s.WeightScalar != 0)
                data.AddLine(GetInvalid(LTransferBad));
        }

        private void VerifyHOMETracker(LegalityAnalysis data, PKM pkm)
        {
            // Tracker value is set via Transfer across HOME.
            // Can't validate the actual values (we aren't the server), so we can only check against zero.
            if (pkm is IHomeTrack home && home.Tracker == 0)
            {
                data.AddLine(Get(LTransferTrackerMissing, ParseSettings.Gen8TransferTrackerNotPresent));
                // To the reader: It seems like the best course of action for setting a tracker is:
                // - Transfer a 0-Tracker pkm to HOME to get assigned a valid Tracker
                // - Don't make one up.
            }
        }

        public IEnumerable<CheckResult> VerifyVCEncounter(PKM pkm, IEncounterable encounter, ILocation transfer, IList<CheckMoveResult> Moves)
        {
            if (pkm.Met_Location != transfer.Location)
                yield return GetInvalid(LTransferMetLocation);
            if (pkm.Egg_Location != transfer.EggLocation)
                yield return GetInvalid(LEggLocationNone);

            // Flag Moves that cannot be transferred
            if (encounter is EncounterStatic s && s.Version == GameVersion.C && s.EggLocation == 256) // Dizzy Punch Gifts
                FlagIncompatibleTransferMove(pkm, Moves, 146, 2); // can't have Dizzy Punch at all

            bool checkShiny = pkm.VC2 || (pkm.TradebackStatus == TradebackType.WasTradeback && pkm.VC1);
            if (!checkShiny)
                yield break;

            if (pkm.Gender == 1) // female
            {
                if (pkm.PersonalInfo.Gender == 31 && pkm.IsShiny) // impossible gender-shiny
                    yield return GetInvalid(LEncStaticPIDShiny, CheckIdentifier.PID);
            }
            else if (pkm.Species == (int)Species.Unown)
            {
                if (pkm.AltForm != 8 && pkm.AltForm != 21 && pkm.IsShiny) // impossibly form-shiny (not I or V)
                    yield return GetInvalid(LEncStaticPIDShiny, CheckIdentifier.PID);
            }
        }

        private static void FlagIncompatibleTransferMove(PKM pkm, IList<CheckMoveResult> Moves, int move, int gen)
        {
            int index = Array.IndexOf(pkm.Moves, move);
            if (index < 0)
                return; // doesn't have move

            var chk = Moves[index];
            if (chk.Generation == gen) // not obtained from a future gen
                Moves[index] = new CheckMoveResult(chk.Source, chk.Generation, Severity.Invalid, LTransferMove, CheckIdentifier.Move);
        }
    }
}
