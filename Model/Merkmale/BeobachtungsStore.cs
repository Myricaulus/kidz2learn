using System.Globalization;
using Kidz2Learn.Entities;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Model.Merkmale;

/// <summary>
///     IndexedDB stores "Beobachtungen" (every answered attempt) and "MerkmalVerlauf" (one
///     profile snapshot per ISO week) behind the reading profile.
/// </summary>
public sealed class BeobachtungsStore(IndexedDb aufgabenDb)
{
    private readonly IndexedDbStore _beobachtungen = aufgabenDb["Beobachtungen"]
        ?? throw new InvalidOperationException("Beobachtungen DB failed to init");

    private readonly IndexedDbStore _verlauf = aufgabenDb["MerkmalVerlauf"]
        ?? throw new InvalidOperationException("MerkmalVerlauf DB failed to init");

    public async Task AddAsync(Beobachtung beobachtung)
    {
        await _beobachtungen.StoreItemAsync(beobachtung);
    }

    public async Task<List<Beobachtung>> GetAllAsync()
    {
        var list = new List<Beobachtung>();
        await foreach (var b in _beobachtungen.GetAllAsync<Beobachtung>())
            list.Add(b);
        return list;
    }

    public async Task<List<MerkmalSchnappschuss>> GetVerlaufAsync()
    {
        var list = new List<MerkmalSchnappschuss>();
        await foreach (var s in _verlauf.GetAllAsync<MerkmalSchnappschuss>())
            list.Add(s);
        return list.OrderBy(s => s.Zeit).ToList();
    }

    /// <summary>Stores the profile as this week's snapshot, replacing an earlier one of the same week.</summary>
    public async Task<MerkmalSchnappschuss> SpeichereWocheAsync(DateTime jetzt, IEnumerable<MerkmalErgebnis> profil)
    {
        var schnappschuss = new MerkmalSchnappschuss
        {
            Id = WochenId(jetzt),
            Zeit = jetzt,
            Merkmale = profil.Select(m => new MerkmalStand(m.Id, m.Wert, m.Status.ToString(), m.N)).ToList()
        };
        await _verlauf.StoreItemAsync(schnappschuss);
        return schnappschuss;
    }

    public static string WochenId(DateTime zeit) =>
        $"{ISOWeek.GetYear(zeit)}-W{ISOWeek.GetWeekOfYear(zeit):00}";
}
