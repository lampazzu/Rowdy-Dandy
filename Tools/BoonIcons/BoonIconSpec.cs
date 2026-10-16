using System;
using System.Collections.Generic;
using System.Drawing;

// Every boon icon, made from the user's own art: crops at native pixel size on a 24 x 24 canvas, some mash-ups.
public static class BoonIconSpec
{
    const string NS = @"Rowdy Dandy\New Stuff you can use\";
    const string RS = @"Rowdy Dandy\RowdyStuff\";
    const string S2 = @"Rowdy Scene 2\";
    const string EQ = @"Rowdy Dandy\Dandy Equipment\";
    const string EP = @"Resources\EnemyPortraits\";
    const string CS = @"Resources\CatSkins\";
    const string GR = @"Resources\Graft\";

    static Bitmap F(string rel, int frames, int k) { return IconKit.Frame(rel, frames, k); }
    static Bitmap W(Bitmap b, double ax = 0.5, double ay = 0.5) { return IconKit.Window(b, ax, ay); }
    static Bitmap C() { return IconKit.Canvas(); }
    static void P(Bitmap d, Bitmap t, int x, int y) { IconKit.Paste(d, t, x, y); }
    static void PC(Bitmap d, Bitmap t, int cx, int cy) { IconKit.PasteCentered(d, t, cx, cy); }
    static Bitmap Flip(Bitmap b) { return IconKit.FlipX(b); }

    // art pieces
    static Bitmap Meatball() { return F(NS + "armondega.aseprite", 1, 0); }
    static Bitmap Egg() { return F(NS + "ovo.aseprite", 1, 0); }
    static Bitmap Bread() { return F(NS + "pao.aseprite", 1, 0); }
    static Bitmap Tomato() { return F(NS + "tomate.aseprite", 1, 0); }
    static Bitmap Cheese() { return F(NS + "guejo.aseprite", 1, 0); }
    static Bitmap Lettuce() { return F(NS + "arface.aseprite", 1, 0); }
    static Bitmap Heart() { return F(@"Rowdy Dandy\Enemies\Pelican\RD_PelicanHeart.png", 1, 0); }
    static Bitmap RowdyHead() { return W(F(RS + "RowdyDandyIdle.png", 12, 0), 0.5, 0.0); }
    static Bitmap Sword() { return IconKit.At(EQ + "WeaponDandy_WK_Sword.png", 10, 0, 141, 31); }
    static Bitmap SwordFlat() { return IconKit.At(EQ + "WeaponDandy_WK_Sword.png", 10, 8, 98, 62); }
    static Bitmap Naginata() { return IconKit.At(EQ + "RowdyDandyForWeapons_Naginata_Neutral.png", 7, 0, 150, 67); }
    static Bitmap Cleaver() { return IconKit.At(EQ + "WeaponDandy_Cleaver.png", 20, 7, 116, 69); }
    static Bitmap Rod() { return IconKit.At(RS + "RowdyV3_N_Attack.png", 10, 8, 24, 30); }
    static Bitmap Insect() { return F(GR + "BRJ_Insect.png", 4, 0); }
    static Bitmap Fire() { return W(F(GR + "BRJ_FireImpact.png", 5, 2), 0.5, 0.6); }
    static Bitmap Sun() { return IconKit.At(GR + "BRJ_FireCircle.png", 5, 2, 34, 26); }
    static Bitmap Wave(int k) { return W(F(NS + "VFX_WaterSonic.png", 9, k), 0.5, 0.5); }
    static Bitmap Cat(string name) { return name.StartsWith("cat_") ? F(EP + name + ".png", 1, 0) : F(CS + "CatPortrait_" + name + ".png", 1, 0); }
    static Bitmap Sparkle() { return F(NS + "STS_Stun.png", 9, 0); }
    static Bitmap Shield() { return F(NS + "STS_Shield.png", 6, 0); }
    static Bitmap Jelly() { return F(S2 + "WaterViva.png", 7, 0); }
    static Bitmap Wolf() { return F(EP + "bigwolf.png", 1, 0); }
    static Bitmap Werewolf() { return IconKit.At(NS + "TDF_SPW_WerewolfuNIT.png", 15, 0, 54, 78); }
    static Bitmap Blood() { return F(NS + "VFX_BloodA.png", 8, 2); }

    // several of one food, in a little pile (3 at the bottom, 2 on them, 1 on top...)
    static Bitmap Pile(Bitmap food, int n)
    {
        Bitmap c = C();
        int w = food.Width, h = food.Height;
        int[] rowCount = { 3, 2, 1 };
        int placed = 0, y = 20 - h;
        for (int r = 0; r < 3 && placed < n; r++)
        {
            int count = Math.Min(rowCount[r], n - placed);
            int total = count * (w + 1) - 1;
            for (int i = 0; i < count; i++) P(c, food, 12 - total / 2 + i * (w + 1), y);
            placed += count;
            y -= h - 1;
        }
        return c;
    }
    static Bitmap Duo(Bitmap a, Bitmap b)
    {
        Bitmap c = C();
        PC(c, a, 8, 9);
        PC(c, b, 16, 15);
        return c;
    }

    static Bitmap One(Bitmap a) { Bitmap c = C(); PC(c, a, 12, 12); return c; }

    public static Dictionary<string, Func<Bitmap>> All()
    {
        var d = new Dictionary<string, Func<Bitmap>>();
        // ---------------- NARCISSISM
        d["hairflip"] = () => RowdyHead();
        d["decoy"] = () => W(F(NS + "STS_Charm.png", 9, 4));
        d["admire"] = () => { Bitmap c = RowdyHead(); P(c, Sparkle(), -6, 0); return c; };
        d["mainchar"] = () => W(F(RS + "RDR_DustLand.png", 11, 2), 0.5, 0.5);
        d["kiss"] = () => One(Heart());
        d["spotlight"] = () => { Bitmap c = RowdyHead(); P(c, Sparkle(), -10, -3); P(c, Flip(Sparkle()), 6, -3); return c; };
        d["jealousy"] = () => { Bitmap c = One(Heart()); PC(c, Blood(), 12, 12); return c; };
        d["flawless"] = () => One(F(NS + "DMG_Ore@Crystals.png", 1, 0));
        d["hairspray"] = () => W(F(RS + "RDR_SilverWax.png", 12, 2));
        // ---------------- SEA ABYSS
        d["wipeout"] = () => Wave(2);
        d["undertow"] = () => Wave(5);
        d["hangten"] = () => W(F(RS + "RowdySurf.png", 4, 0), 0.5, 1.0);
        d["saltwater"] = () => W(F(NS + "TBZG_VFX_ManaRecovery.png", 10, 6));
        d["pressure"] = () => W(F(NS + "FX_GroundPound.png", 12, 3));
        d["angler"] = () => { Bitmap c = One(Jelly()); return c; };
        d["endlesssurf"] = () => Wave(4);
        d["ripcurrent"] = () => Wave(7);
        d["leviathan"] = () => Duo(Jelly(), Flip(Jelly()));
        // ---------------- LYCANTHROPY
        d["feral"] = () => W(F(NS + "FX_CapeloboSlash.png", 6, 0));
        d["moonrage"] = () => One(Wolf());
        d["bloodthirst"] = () => One(F(NS + "VFX_BloodDirectional.png", 4, 1));
        d["packleader"] = () => One(IconKit.Outline(Cat("cat_Samurai"), Color.FromArgb(255, 230, 40, 60)));
        d["moon"] = () => Werewolf();
        d["huntmark"] = () => W(F(NS + "TBZG_Pointer.png", 9, 0), 0.5, 0.0);
        d["pounce"] = () => W(F(NS + "FX_CapeloboSlash.png", 6, 2));
        d["openwounds"] = () => One(Blood());
        d["alpharoar"] = () => W(F(NS + "STS_Fear.png", 9, 5));
        d["feast"] = () => Duo(Wolf(), Meatball());
        d["silverfur"] = () => W(F(RS + "RDR_SilverDust 1.png", 1, 0));
        // ---------------- STEPHMOSS
        d["sporestep"] = () => One(F(S2 + "RDR_Flowers.png", 3, 1));
        d["rottenedge"] = () => W(F(NS + "TBZG_VFX_Poison.png", 10, 4));
        d["overgrowth"] = () => W(F(@"Scenery\RDR Assets\RDR_EarthPillar.png", 1, 0), 0.5, 0.3);
        d["compost"] = () => W(F(NS + "TBZG_VFX_Heal.png", 10, 4));
        d["fester"] = () => One(Insect());
        d["plague"] = () => { Bitmap c = C(); PC(c, Insect(), 7, 8); PC(c, Flip(Insect()), 17, 11); PC(c, Insect(), 10, 18); return c; };
        d["thornskin"] = () => W(F(S2 + "RDR_Vine.png", 1, 0), 0.2, 0.4);
        d["rootsnare"] = () => W(F(@"Scenery\Assets\Interactable Assets\RDR_PlantPurple.png", 18, 0), 0.5, 0.9);
        d["stephmoss"] = () => W(F(GR + "BRJ_Protobug_Idle.png", 4, 0), 0.5, 0.25);
        d["bloom"] = () => One(F(S2 + "RDR_Flowers.png", 3, 0));
        d["sporelob"] = () => W(F(@"Scenery\Assets\Interactable Assets\RDR_PlantPurple.png", 18, 0), 0.5, 0.2);
        // ---------------- STINKY BOIS GUILD
        d["catnip"] = () => One(F(S2 + "RDR_Flowers.png", 3, 2));
        d["ninelives"] = () => One(Cat("cat_Samurai"));
        d["furcoat"] = () => One(Cat("Mushidon"));
        d["felinefury"] = () => One(Cat("cat_Wig"));
        d["catcall"] = () => One(Cat("Tchogon"));
        d["straytax"] = () => W(F(NS + "PIV_Inter_Crates.png", 6, 2), 0.3, 0.6);
        d["hairball"] = () => W(F(RS + "RDR_DustLand.png", 11, 6));
        d["ambush"] = () => One(Cat("Paprika"));
        d["stench"] = () => One(Cat("Lallo"));
        d["topcat"] = () => One(Cat("ThePeak"));
        d["catloyalty"] = () => Duo(Cat("cat_Wig"), Cat("cat_Samurai"));
        // ---------------- CRAZY CHEF
        d["sandwich"] = () => { Bitmap c = C(); PC(c, Bread(), 12, 16); PC(c, Cheese(), 12, 13); PC(c, Tomato(), 12, 10); PC(c, Lettuce(), 12, 8); PC(c, Meatball(), 12, 5); return c; };
        d["meatball"] = () => Pile(Meatball(), 6);
        d["eggs"] = () => Pile(Egg(), 3);
        d["tomato"] = () => Pile(Tomato(), 5);
        d["cheese"] = () => Pile(Cheese(), 3);
        d["secretsauce"] = () => { Bitmap c = C(); PC(c, Blood(), 12, 12); PC(c, Tomato(), 12, 15); return c; };
        d["foodfight"] = () => { Bitmap c = C(); PC(c, Bread(), 7, 15); PC(c, Egg(), 16, 8); PC(c, Tomato(), 17, 17); return c; };
        d["foodstock"] = () => { Bitmap c = C(); Bitmap[] f = { Meatball(), Egg(), Tomato(), Cheese(), Bread(), Lettuce(), Meatball(), Tomato(), Egg() }; for (int i = 0; i < 9; i++) PC(c, f[i], 5 + (i % 3) * 7, 5 + (i / 3) * 7); return c; };
        d["satisfied"] = () => { Bitmap c = C(); PC(c, Bread(), 9, 14); PC(c, Egg(), 15, 10); PC(c, Cheese(), 15, 17); return c; };
        d["sandwichrain"] = () => { Bitmap c = C(); Bitmap[] f = { Bread(), Tomato(), Cheese(), Lettuce(), Meatball(), Egg() }; int[] xs = { 4, 12, 20, 7, 16, 11 }, ys = { 4, 6, 3, 13, 15, 20 }; for (int i = 0; i < 6; i++) PC(c, f[i], xs[i], ys[i]); return c; };
        // ---------------- THE BLACKSMITH
        d["weaponsnob"] = () => { Bitmap c = W(F(NS + "PIV_GunImpact.png", 1, 0)); P(c, Sword(), 0, 0); return c; };
        d["whetstone"] = () => One(F(NS + "DMG_Ore@Sapphire.png", 1, 0));
        d["tempered"] = () => One(F(NS + "DMG_Ore@Ruby.png", 1, 0));
        d["hookline"] = () => Rod();
        d["splitedge"] = () => SwordFlat();
        d["skewer"] = () => Naginata();
        d["butcher"] = () => Cleaver();
        d["gearedup"] = () => One(Shield());
        d["justrod"] = () => { Bitmap c = Rod(); P(c, Sparkle(), -8, -2); return c; };
        d["sharp"] = () => { Bitmap c = Sword(); P(c, Sparkle(), -10, 10); return c; };
        d["dizzy"] = () => One(Sparkle());
        d["swordcharge"] = () => { Bitmap c = Fire(); P(c, Sword(), 0, 0); return c; };
        d["skybeam"] = () => Flip(Sword());
        d["onrush"] = () => { Bitmap c = IconKit.At(EQ + "RowdyDandyForWeapons_Naginata_Neutral.png", 7, 0, 138, 67); P(c, Sparkle(), -6, 12); return c; };
        d["quake"] = () => W(F(NS + "FX_GroundPound.png", 12, 6), 0.5, 0.7);
        d["apron"] = () => { Bitmap c = One(Shield()); return c; };
        d["slices"] = () => IconKit.At(EQ + "WeaponDandy_Cleaver.png", 20, 9, 152, 62);
        d["armory"] = () => { Bitmap c = C(); Bitmap s = Sword(); P(c, s, -6, 0); P(c, s, 0, 2); P(c, s, 6, 0); return c; };
        d["senbonzakura"] = () => W(F(S2 + "RDR_Vine.png", 1, 0), 0.6, 0.4);
        // ---------------- THE SUN GOD
        d["solarflare"] = () => IconKit.At(GR + "BRJ_FireCircle.png", 5, 2, 82, 96);
        d["sunburn"] = () => Fire();
        d["daybreak"] = () => Sun();
        d["halo"] = () => W(F(RS + "RDR_DustLand.png", 11, 3));
        d["blinding"] = () => { Bitmap c = One(F(NS + "STS_Stun.png", 9, 2)); P(c, Sparkle(), 0, -6); return c; };
        d["sunspot"] = () => IconKit.At(GR + "BRJ_FireCircle.png", 5, 2, 30, 168);
        d["cometothelight"] = () => { Bitmap c = Sun(); P(c, Sparkle(), -6, 8); return c; };
        // ---------------- DUOS
        d["beachbod"] = () => W(F(@"Misc\Test\testWave.png", 1, 0), 0.5, 0.4);
        d["redtide"] = () => Wave(6);
        d["wolfwhistle"] = () => Duo(Wolf(), Heart());
        d["wolfpack"] = () => Duo(Wolf(), Cat("cat_Wig"));
        d["glamourpuss"] = () => { Bitmap c = One(Cat("Paprika")); P(c, Sparkle(), -8, -2); return c; };
        d["eclipse"] = () => Duo(Sun(), Wolf());
        d["forgefire"] = () => { Bitmap c = Fire(); P(c, SwordFlat(), 0, -3); return c; };
        d["catfood"] = () => Duo(Cat("Mushidon"), Meatball());
        d["fermented"] = () => Duo(Tomato(), Insect());
        d["silverclaws"] = () => W(F(NS + "FX_CapeloboSlash.png", 6, 3));
        d["jellypals"] = () => Duo(Jelly(), Cat("Tchogon"));
        d["peakplate"] = () => Duo(Cat("ThePeak"), Shield());
        d["scavenger"] = () => { Bitmap c = W(F(NS + "PIV_Inter_Crates.png", 6, 2), 0.3, 0.6); P(c, Cat("Lallo"), 8, 8); return c; };
        d["clawsharpener"] = () => Duo(Cat("cat_Wig"), F(NS + "DMG_Ore@Sapphire.png", 1, 0));
        d["toxicbeauty"] = () => Duo(Heart(), Insect());
        d["foodcritic"] = () => { Bitmap c = RowdyHead(); PC(c, Egg(), 17, 18); return c; };
        d["fishingcats"] = () => { Bitmap c = Wave(1); PC(c, Cat("cat_Wig"), 12, 12); return c; };
        d["rabid"] = () => Duo(Wolf(), Insect());
        d["moldcats"] = () => Duo(Cat("Lallo"), Insect());
        d["rustededge"] = () => { Bitmap c = Sword(); PC(c, Insect(), 15, 17); return c; };
        d["photosynthesis"] = () => Duo(Sun(), F(S2 + "RDR_Flowers.png", 3, 1));
        d["sunbathing"] = () => Duo(Sun(), Cat("Paprika"));
        d["midnightsnack"] = () => Duo(Werewolf(), Meatball());
        return d;
    }

    public static string Build(string outDir, string sheetPath)
    {
        var made = new List<KeyValuePair<string, Bitmap>>();
        var errors = new System.Text.StringBuilder();
        foreach (var kv in All())
        {
            try
            {
                Bitmap b = kv.Value();
                if (b.Width != IconKit.N || b.Height != IconKit.N) b = IconKit.Window(b);
                b = IconKit.Circle(b, IconKit.N / 2.0 - 0.3); // round, to sit in the round medallion
                IconKit.Save(b, System.IO.Path.Combine(outDir, kv.Key + ".png"));
                made.Add(new KeyValuePair<string, Bitmap>(kv.Key, b));
            }
            catch (Exception e) { errors.AppendLine(kv.Key + ": " + e.Message); }
        }
        IconKit.Sheet(made, sheetPath, 12);
        return made.Count + " icons. " + errors;
    }
}
