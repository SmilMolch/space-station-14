namespace Content.Shared.Roles.RoleCodeword;

public abstract class SharedRoleCodewordSystem : EntitySystem
{
    public void SetRoleCodewords(Entity<RoleCodewordComponent> ent, string key, List<string> codewords, Color color)
    {
        var data = new CodewordsData(color, codewords);
        ent.Comp.RoleCodewords[key] = data;
        Dirty(ent);
    }

    // SS220-removeantag-begin
    /// <summary>Only removes one role's codewords; other antag keys are preserved.</summary>
    public bool RemoveRoleCodewords(Entity<RoleCodewordComponent> ent, string key)
    {
        if (!ent.Comp.RoleCodewords.Remove(key))
            return false;
        Dirty(ent);
        return true;
    }
    // SS220-removeantag-end
}
