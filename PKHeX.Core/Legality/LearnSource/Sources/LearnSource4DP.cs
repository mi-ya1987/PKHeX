using System;
using System.Diagnostics.CodeAnalysis;
using static PKHeX.Core.LearnMethod;
using static PKHeX.Core.LearnEnvironment;
using static PKHeX.Core.LearnSource4;

namespace PKHeX.Core;

/// <summary>
/// Exposes information about how moves are learned in <see cref="DP"/>.
/// </summary>
public sealed class LearnSource4DP : ILearnSource<PersonalInfo4>, IEggSource
{
    public static readonly LearnSource4DP Instance = new();
    private static readonly PersonalTable4 Personal = PersonalTable.DP;
    private static readonly Learnset[] Learnsets = Legal.LevelUpDP;
    private static readonly EggMoves6[] EggMoves = Legal.EggMovesDPPt;
    private const int MaxSpecies = Legal.MaxSpeciesID_4;
    private const LearnEnvironment Game = DP;
    private const int Generation = 4;
    private const int CountTM = 92;

    public Learnset GetLearnset(ushort species, byte form) => Learnsets[Personal.GetFormIndex(species, form)];

    public bool TryGetPersonal(ushort species, byte form, [NotNullWhen(true)] out PersonalInfo4? pi)
    {
        pi = null;
        if (species > MaxSpecies)
            return false;
        pi = Personal[species, form];
        return true;
    }

    public bool GetIsEggMove(ushort species, byte form, ushort move)
    {
        if (species > MaxSpecies)
            return false;
        var moves = EggMoves[species];
        return moves.GetHasEggMove(move);
    }

    public ReadOnlySpan<ushort> GetEggMoves(ushort species, byte form)
    {
        if (species > MaxSpecies)
            return ReadOnlySpan<ushort>.Empty;
        return EggMoves[species].Moves;
    }

    public MoveLearnInfo GetCanLearn(PKM pk, PersonalInfo4 pi, EvoCriteria evo, ushort move, MoveSourceType types = MoveSourceType.All, LearnOption option = LearnOption.Current)
    {
        if (types.HasFlag(MoveSourceType.LevelUp))
        {
            var learn = GetLearnset(evo.Species, evo.Form);
            var level = learn.GetLevelLearnMove(move);
            if (level != -1 && level <= evo.LevelMax)
                return new(LevelUp, Game, (byte)level);
        }

        if (types.HasFlag(MoveSourceType.Machine))
        {
            if (GetIsTM(pi, move))
                return new(TMHM, Game);
            if ((move is (int)Move.Defog || pk.Format == Generation) && GetIsHM(pi, move))
                return new(TMHM, Game);
        }

        if (types.HasFlag(MoveSourceType.TypeTutor) && GetIsTypeTutor(evo.Species, move))
            return new(Tutor, Game);

        if (types.HasFlag(MoveSourceType.SpecialTutor) && GetIsSpecialTutor(pi, move))
            return new(Tutor, Game);

        return default;
    }

    private static bool GetIsTypeTutor(ushort species, ushort move)
    {
        var index = Array.IndexOf(SpecialTutors_4, move);
        if (index == -1)
            return false;
        var list = SpecialTutors_Compatibility_4[index].AsSpan();
        return list.IndexOf(species) != -1;
    }

    private static bool GetIsSpecialTutor(PersonalInfo4 pi, ushort move)
    {
        var index = Array.IndexOf(Tutors_4, move);
        if (index == -1)
            return false;
        return pi.TypeTutors[index];
    }

    private static bool GetIsTM(PersonalInfo4 info, ushort move)
    {
        var index = Array.IndexOf(TM_4, move);
        return info.GetIsLearnTM(index);
    }

    private static bool GetIsHM(PersonalInfo4 info, ushort move)
    {
        var index = Array.IndexOf(HM_DPPt, move);
        return info.GetIsLearnTM(CountTM + index);
    }

    public void GetAllMoves(Span<bool> result, PKM pk, EvoCriteria evo, MoveSourceType types = MoveSourceType.All)
    {
        if (!TryGetPersonal(evo.Species, evo.Form, out var pi))
            return;

        if (types.HasFlag(MoveSourceType.LevelUp))
        {
            var learn = GetLearnset(evo.Species, evo.Form);
            (bool hasMoves, int start, int end) = learn.GetMoveRange(evo.LevelMax);
            if (hasMoves)
            {
                var moves = learn.Moves;
                for (int i = end; i >= start; i--)
                    result[moves[i]] = true;
            }
        }

        if (types.HasFlag(MoveSourceType.Machine))
        {
            pi.SetAllLearnTM(result, TM_4);

            if (pk.Format == Generation)
                pi.SetAllLearnHM(result, HM_DPPt);
            else if (pi.GetIsLearnHM(4)) // Permit Defog to leak through if transferred to Gen5+ (via HG/SS)
                result[(int)Move.Defog] = true;
        }

        if (types.HasFlag(MoveSourceType.SpecialTutor))
            pi.SetAllLearnTutorType(result, Tutors_4);
    }
}
