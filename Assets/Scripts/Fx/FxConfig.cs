using UnityEngine;

/// <summary>
/// Shared assets for the in-game visual effects (GameFx) and the dice animation.
/// Lives in Resources/FxConfig.asset so the materials/sprites are always included in builds.
/// Created by PPOP/Fx/Build Fx Assets; every field can be replaced with custom art.
/// </summary>
[CreateAssetMenu(fileName = "FxConfig", menuName = "PPOP/Fx Config")]
public class FxConfig : ScriptableObject
{
    public const string ResourcePath = "FxConfig";

    [Tooltip("Additive particles (soft circle texture).")]
    public Material particleMaterial;

    [Tooltip("Alpha-blended flat ring drawn on the board (turn marker, shockwaves).")]
    public Material ringMaterial;

    [Tooltip("Additive rounded rectangle: fluorescent tubes of the central maze (BackroomsAmbience).")]
    public Material tubeMaterial;

    [Header("UI sprites")]
    public Sprite softCircle;
    public Sprite roundedSquare;
    public Sprite diceFace;
    public Sprite pip;

    [Tooltip("Picture of the entity / event card in the combat VS panel. Replace Art/Fx/Entidad.png.")]
    public Sprite entityPortrait;

    private static FxConfig cached;

    public static FxConfig Instance
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<FxConfig>(ResourcePath);

            return cached;
        }
    }
}
