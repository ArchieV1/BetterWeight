using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

/// <summary>
/// This version will just add weights for building and nothing else
/// No recipes need touching
/// </summary>
namespace ArchieVBetterWeight2
{
    /// <summary>
    /// Call other functions to start the program
    /// Workaround for the other classes needing to be static
    /// </summary>
    [StaticConstructorOnStartup]
    public static class StartupClass2
    {
        static void Start()
        {
            Log.Error("StartupClass2.Start");
            BetterWeight2 betterWeight2 = new BetterWeight2();
        }
    }

    

    public class BetterWeight
    {
        public List<RecipeProductIngredient> recipeList = CreateRecipeList();
        public float efficiency = 0.40f;

        public BetterWeight() //Constructor
        {
            Log.Error("BetterWeightConstructor");

            Dictionary<ThingDef, float> dictionary = GenerateListOfBaseMaterials(this.efficiency);

            foreach (KeyValuePair<ThingDef, float> pair in dictionary)
            {
                Log.Message(pair.Key.defName + pair.Value.ToString());
            }
            UpdateThingDefMasses(dictionary);

            ThingDef thing = DefDatabase<ThingDef>.GetNamed("Hay");
            //ArrayListArrayListToTxt(FindRecipiesForThing(thing);

            //TestingOutputToLog();

            //TestingGroundFileOutput();

            //Log.Message(DoesRecipeExist(thing, CreateRecipeList()).ToString());
            //Log.Message("BetterWeight Loaded Sucessfully");
        }

        //Find if object is a base material (BaseMaterial)
        //true for yes or false for no
        public Boolean IsThingBaseMaterial(ThingDef thing)
        {
            Log.Error("IsThingBaseMaterial");
            //If not a building and no recipe then it IS a base material
            try
            {
                if (thing.costList == null && !DoesRecipeExist(thing, this.recipeList)) { return true; }
                else { return false; }
            }
            catch (NullReferenceException e)
            {
                Log.Message(e.ToString());
                return true;
            }
        }

        //Search all objects and return dict of "name, newMass" to be put into an xml patch maker
        public Dictionary<ThingDef, float> GenerateListOfBaseMaterials(float efficiency)
        {
            Log.Error("GenerateListOfBaseMaterials");
            Dictionary<ThingDef, float> dictionary = new Dictionary<ThingDef, float>();
            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                float mass = thing.BaseMass;
                bool baseMaterial = IsThingBaseMaterial(thing);

                //If not a base material calculate the mass
                if (!baseMaterial)
                {
                    mass = FindMass(thing, efficiency);
                }

                // Add final obj and mass to the dictionary
                dictionary.Add(thing, mass);
            }
            return dictionary;
        }

        public float FindMass(ThingDef thing, float efficiency)
        {
            float Mass = 0.00f;
            if (IsThingBaseMaterial(thing))
            {
                return thing.BaseMass;
            }
            else
            {
                foreach (ThingDefCountClass part in thing.costList)
                {
                    // If base material just add the mass otherwise FindMass of the object then add that. So recursion
                    if (IsThingBaseMaterial(part.thingDef))
                    {
                        Mass += part.count * part.thingDef.BaseMass * efficiency;
                    }
                    else
                    {
                        Mass += part.count * FindMass(part.thingDef, efficiency) * efficiency;
                    }
                }
                return Mass;
            }

        }

        //Confident in this working
        //Does a recipe exist with this thing as the product
        public Boolean DoesRecipeExist(ThingDef thing, List<RecipeProductIngredient> recipeList)
        {
            foreach (RecipeProductIngredient productIngredient in recipeList)
            {
                foreach (ThingDefCountClass product in productIngredient.getProducts())
                    if (product.thingDef.defName.Equals(thing.defName))
                    {

                        //This foreach is testing code
                        foreach (IngredientCount ingredient in productIngredient.getIngredients())
                        {
                            Log.Error(ingredient.ToString());
                        }
                        return true;
                    }
            }
            return false;
        }

        //Confident in this working
        //Get list of objects that store both ingredients and products of a recipe
        public static List<RecipeProductIngredient> CreateRecipeList()
        {
            List<RecipeProductIngredient> recipeList = new List<RecipeProductIngredient>();

            List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            foreach (RecipeDef recipe in allRecipes)
            {
                RecipeProductIngredient recipePair = new RecipeProductIngredient();
                recipePair.SetProducts(recipe.products);
                recipePair.SetIngredients(recipe.ingredients);

                recipeList.Add(recipePair);
            }
            return recipeList;
        }
        //Returns an arraylist of arraylists. Each deep arraylist contains a list of "IngredientCount" objects of the recipe. The first is the number of products
        //Example arraylist = [[1, (0.25x ingredients), (0.25x vegetarian)], [4, (1x ingredients), (1x vegetarian)]]
        //After using this function you *must* check to see if the arraylist actaully has anything in it. If it fails to find a recipe it will be null.
        //Example if no recipces found = []
        //A SINGLE arraylist without another inside of it

        //Quite sure this works
        public ArrayList FindRecipiesForThing(ThingDef thing)
        {
            Log.Warning("FindRecipiesForThing");
            ArrayList listOfIngredientLists = new ArrayList();

            IEnumerable<RecipeDef> allrecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            foreach (RecipeDef recipe in allrecipes)
            {
                if (recipe.products.Count > 0)
                {
                    ThingDefCountClass product = recipe.products[0];
                    Log.Error(thing.defName);
                    if (product.thingDef.defName.Equals(thing.defName))
                    {
                        //Crash prob here
                        Log.Warning("product name = thing name");
                        Log.Message(thing.defName);
                        //ArrayList ingredientList = new ArrayList();
                        //ingredientList.Add(product.count);
                        foreach (IngredientCount ingredient in recipe.ingredients)
                        {
                            Log.Message(ingredient.ToString());
                            //ingredientList.Add(ingredient);
                        }
                        //listOfIngredientLists.Add(ingredientList);
                    }
                }
            }
            return listOfIngredientLists;
        }

        //Update a def
        public static void UpdateThingDefMass(ThingDef thing, float mass)
        {
            using (StreamWriter file = new StreamWriter("BetterWeightsPatch.xml"))
            {
                string operation = "";
                if (thing.BaseMass == 0.00f)
                {
                    operation = "PatchOperationAdd";
                }
                else
                {
                    operation = "PatchOperationReplace";
                }

                //Writes the patch for the given ThingDef
                file.WriteLine("<Operation Class=\"" + operation + "\">");
                file.WriteLine("< xpath >//[defName = \"" + thing.defName + "\"] / statBases </ xpath >");
                file.WriteLine("<value>");
                file.WriteLine("    < Mass > " + mass + " </ Mass >");
                file.WriteLine("</ value >");
                file.WriteLine("</Operation>");
            }
        }

        //Update dictionary of defs using their floar pairs
        public static void UpdateThingDefMasses(Dictionary<ThingDef, float> dictionary)
        {
            StreamWriter file = new StreamWriter("BetterWeightsPatch.xml");
            file.WriteLine("<?xml version=\"1.0\" encoding=\"utf - 8\" ?>");
            file.WriteLine("<Patch>");

            foreach (KeyValuePair<ThingDef, float> entry in dictionary)
            {
                UpdateThingDefMass(entry.Key, entry.Value);
            }

            file.WriteLine("</Patch>");
        }


        //Testing stuff
        public static void TestingGroundFileOutput()
        {
            //ThingDef thing = DefDatabase<ThingDef>.GetNamed("ComponentIndustrial");
            //RecipeDef def = DefDatabase<RecipeDef>.GetNamed("Make_ComponentIndustrial");
            IEnumerable<RecipeDef> allrecipes = DefDatabase<RecipeDef>.AllDefs;
            using (StreamWriter file = new StreamWriter("testFile.txt"))
            {
                foreach (RecipeDef recipe in allrecipes)
                {
                    //file.WriteLine(thing.defName);
                    if (recipe.products != null)
                    {
                        foreach (ThingDefCountClass product in recipe.products)
                        {
                            file.WriteLine(product);
                        }
                        file.WriteLine("");
                        foreach (IngredientCount ingred in recipe.ingredients)
                        {
                            file.WriteLine(ingred);
                        }
                        file.WriteLine("\n\n");
                    }
                }
            }
        }
        public static void ArrayListArrayListToTxt(ArrayList arrayListArrayList)
        {
            using (StreamWriter file = new StreamWriter("testFile.txt"))
            {
                foreach (ArrayList arrayList in arrayListArrayList)
                {
                    foreach (var line in arrayList)
                    {
                        file.WriteLine(line);
                    }
                }
            }
        }
        public static void TestingOutputToLog()
        {
            ThingDef thing = DefDatabase<ThingDef>.GetNamed("Beer");
            IEnumerable<RecipeDef> allrecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            foreach (RecipeDef recipe in allrecipes)
            {
                foreach (ThingDefCountClass product in recipe.products)
                {
                    //Log.Message(product.thingDef.defName);
                    Log.Error(product.ToString());
                    if (product.thingDef.defName.Equals(thing.defName))
                    {
                        //Crash prob here
                        Log.Warning("product name = thing name");
                        Log.Message(thing.defName);
                        ArrayList ingredientList = new ArrayList();
                        ingredientList.Add(product.count);
                        foreach (IngredientCount ingredient in recipe.ingredients)
                        {
                            Log.Message(ingredient.ToString());
                            ingredientList.Add(ingredient);
                        }
                    }
                }
            }
        }

        public static int NumberOfProducts()
        {
            IEnumerable<RecipeDef> allrecipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            foreach (RecipeDef recipe in allrecipes)
            {
                if (recipe.products.Count > 0)
                {
                    Log.Error(recipe.products[0].ToString());
                    foreach (ThingDefCountClass product in recipe.products)
                    {
                        Log.Message(product.ToString());
                    }
                }
                else
                {
                    Log.Error("Count 0");
                }
            }
            return 1;
        }
    }
}

public class RecipeProductIngredient
{
    //Confident in this working
    //Stores product and ingredient List
    public List<ThingDefCountClass> product;
    public List<IngredientCount> ingredients;

    public void SetProducts(List<ThingDefCountClass> product) { this.product = product; }
    public void SetIngredients(List<IngredientCount> ingredients) { this.ingredients = ingredients; }

    public List<ThingDefCountClass> getProducts() { return product; }
    public List<IngredientCount> getIngredients() { return ingredients; }
}
