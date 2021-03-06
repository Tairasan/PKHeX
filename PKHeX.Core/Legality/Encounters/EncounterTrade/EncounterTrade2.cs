﻿namespace PKHeX.Core
{
    public sealed class EncounterTrade2 : EncounterTradeGB
    {
        public override int Generation => 2;

        public EncounterTrade2(int species, int level, int tid) : base(species, level)
        {
            TID = tid;
            Version = GameVersion.GSC; // All share the same details
        }

        public override bool IsMatch(PKM pkm)
        {
            if (Level > pkm.CurrentLevel) // minimum required level
                return false;
            if (TID != pkm.TID)
                return false;

            if (pkm.Format <= 2)
            {
                if (Gender >= 0 && Gender != pkm.Gender)
                    return false;
                if (IVs.Count != 0 && !Legal.GetIsFixedIVSequenceValidNoRand(IVs, pkm))
                    return false;
                if (pkm.Format == 2 && pkm.Met_Location != 0 && pkm.Met_Location != 126)
                    return false;
            }

            if (!IsValidTradeOTGender(pkm))
                return false;
            return IsValidTradeOTName(pkm);
        }

        private bool IsValidTradeOTGender(PKM pkm)
        {
            if (OTGender == 1)
            {
                // Female, can be cleared if traded to RBY (clears met location)
                if (pkm.Format <= 2)
                    return pkm.OT_Gender == (pkm.Met_Location != 0 ? 1 : 0);
                return pkm.OT_Gender == 0 || !pkm.VC1; // require male except if transferred from GSC
            }
            return pkm.OT_Gender == 0;
        }

        private bool IsValidTradeOTName(PKM pkm)
        {
            var OT = pkm.OT_Name;
            if (pkm.Japanese)
                return GetOT((int)LanguageID.Japanese) == OT;
            if (pkm.Korean)
                return GetOT((int)LanguageID.Korean) == OT;

            const int start = (int)LanguageID.English;
            const int end = (int)LanguageID.Spanish;

            for (int i = start; i <= end; i++)
            {
                if (TrainerNames[i] == OT)
                    return true;
            }
            return false;
        }
    }
}
