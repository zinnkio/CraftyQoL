using System.Collections.Generic;
using CraftyQoL.Core;

namespace CraftyQoL
{
    internal sealed class RecipeMoveContext
    {
        private ObjectDB? _db;
        private int _count;
        private readonly List<RecipeMove> _approved=new List<RecipeMove>();
        internal IReadOnlyList<RecipeMove> Approved()
        {
            ObjectDB db=ObjectDB.instance;
            if(!db){_db=null;_approved.Clear();return _approved;}
            if(_db==db && _count==db.m_recipes.Count)return _approved;
            _db=db;_count=db.m_recipes.Count;_approved.Clear();
            foreach(var move in RecipeMoves.All)
            {
                Recipe? match=null;var seen=new HashSet<Recipe>();
                foreach(Recipe recipe in db.m_recipes)
                {
                    if(!recipe || !recipe.m_item || !seen.Add(recipe) || recipe.name!=move.Recipe || recipe.m_item.gameObject.name!=move.Output || !recipe.m_craftingStation || recipe.m_craftingStation.gameObject.name!=RecipeMove.NewStation)continue;
                    if(match){match=null;break;} // A colliding identity must never be migrated.
                    match=recipe;
                }
                if(!match || !match!.m_enabled || match.m_noCraftOnlyUpgrade)continue;
                try { if(move.Approves(RecipeMetadata.Key(match),TrackerCatalogue.Fingerprint(match),match.m_enabled,match.m_noCraftOnlyUpgrade,true))_approved.Add(move); }
                catch(System.Exception e) when(e is System.IO.InvalidDataException || e is System.ArgumentException || e is System.NullReferenceException)
                { /* Incomplete/modded metadata remains unmigrated. */ }
            }
            return _approved;
        }
    }
}
