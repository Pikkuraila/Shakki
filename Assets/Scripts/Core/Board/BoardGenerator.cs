using UnityEngine;
using Shakki.Core;

public class BoardGenerator : MonoBehaviour
{
    public BoardTemplateSO template;
    public int? seedOverride; // Inspectorista voi antaa seediä

    public GameState BuildGame()
    {
        var allowed = template.BuildAllowedMask();
        var tags = template.BuildTags(allowed, seedOverride);

        var geom = new GridGeometry(template.width, template.height, allowed);
        var gs = new GameState(geom, tags);
        return gs;
    }
}