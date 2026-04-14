/// <summary>
/// Custom zone type for the Skyreader Guild HQ.
/// Must live in the global namespace so Elin's ClassCache can resolve it
/// via Type.GetType("Zone_SkyreaderGuild, AssemblyName").
/// </summary>
public class Zone_SkyreaderGuild : Zone_Civilized
{
    public override bool ShouldRegenerate => false;

    public override bool HasLaw => true;

    public override bool AllowCriminal => false;

    public override bool RestrictBuild => true;

    public override bool CanDigUnderground => false;

    public override bool UseFog => true;

    public override bool HiddenInRegionMap => true;

    public override bool IsExplorable => false;

    public override ZoneTransition.EnterState RegionEnterState => ZoneTransition.EnterState.Center;

    public override float PrespawnRate => 1.0f;

    public override float OreChance => 0f;

    public override float ShrineChance => 0f;

    public override void OnCreateBP()
    {
        bp.map = new Map();
        bp.map.CreateNew(SkyreaderGuild.GuildLayoutBuilder.MapSize);
        bp.map.config.indoor = true;
        bp.map.config.idBiome = "Plain";
        bp.map.config.idSceneTemplate = "Indoor";
        bp.map.config.embarkX = 9;
        bp.map.config.embarkY = 24;
        map = bp.map;
    }

    public override void OnGenerateMap()
    {
        SkyreaderGuild.GuildLayoutBuilder.Build(this);
    }
}
