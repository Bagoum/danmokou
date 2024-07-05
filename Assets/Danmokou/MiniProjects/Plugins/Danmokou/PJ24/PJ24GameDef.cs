using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN;
using MiniProjects.PJ24;
using MiniProjects.VN;
using Newtonsoft.Json;
using Suzunoya;
using Suzunoya.ADV;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;
using static SuzunoyaUnity.Helpers;
using Vector3 = System.Numerics.Vector3;
using static MiniProjects.PJ24.Item;
using static MiniProjects.PJ24.Effect;
using static MiniProjects.PJ24.Trait;

namespace MiniProjects.PJ24 {

[CreateAssetMenu(menuName = "Data/ADV/PJ24 Game")]
public class PJ24GameDef : ADVGameDef {
    public class Executing : DMKExecutingADV<Executing.PJ24IdealizedState, PJ24ADVData> {
        private readonly PJ24GameDef gdef;

        public Executing(PJ24GameDef gdef, ADVInstance inst) : base(inst) {
            this.gdef = gdef;
            tokens.Add(Md.MinimalState.AddConst(true));
        }

        /// <inheritdoc/>
        public override void ADVDataFinalized() {
            ServiceLocator.Find<PJ24CraftingUXML>().SetupCB.SetSecond(this);
        }

        public const string House = "myhouse";
        private SZYUCharacter M => VN.Find<Marisa>();
        private Cancellable? intuitionCt = null;

        public Task RunCraftedItemIntuition(ItemInstance item) {
            return Manager.ExecuteVN(Context("", async () => {
                Md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
                await M.SayC($"Poggers! I crafted {item.Type.Name}!");
            }, true, Cancellable.Replace(ref intuitionCt)), true);
        }

        //base menu intuition may conflict with VN executions from assertions; in these cases,
        // we want to ignore the intuition, so we use tryExecute
        public void TryRunBaseMenuIntuition() { 
            Manager.TryExecuteVN(Context("", async () => {
                //using var _links = LinkCallback.RegisterClicker(("link0", "example text"), ("link1", "this is a tooltip"));
                Md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
                if (Data.Phase.RecommendedNext is { } rec) {
                    var seq = Data.CanProbablySatisfy(rec) ?
                        "I think I can submit that request now." :
                        "Maybe I should work on that first.";
                    await M.Say($"{rec.Requestor} asked me to craft {rec.Required.Type.Name}.\n{seq}");
                } else if (Data.Phase.PhaseComplete) {
                    await M.Say(
                        $"I've finished everything for this phase. I can do whatever I want until the deadline.");
                } else {
                    await M.Say("I've finished everything on hand, but I suspect that I'll get another request soon.");
                }
                await vn.Wait(() => false);
            }, true, Cancellable.Replace(ref intuitionCt)), true);
        }

        public void RunSynthMenuCraftCtIntuition(Recipe craft, int maxCt) {
            Manager.ExecuteVN(Context("", async () => {
                Md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
                var saveTime = craft.Time < 1 ?
                    "\nI can save time if I craft in bigger batches." :
                    "";
                await M.Say($"How many {craft.Result.Name} should I craft? " +
                            $"I have enough ingredients to craft {maxCt}.{saveTime}");
                await vn.Wait(() => false);
            }, true, Cancellable.Replace(ref intuitionCt)), true);
        }
        
        public void RunSynthIngredientSelIntuition(Recipe craft) {
            Manager.ExecuteVN(Context("", async () => {
                Md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
                var msg = $"I need to select the ingredients for {craft.Result.Name}.";
                if (craft.Effects is {} eff)
                    foreach (var (i, x) in eff.Enumerate()) {
                        if (x != null)
                            msg += $"\nI can get some effects depending on the quality " +
                                   $"of the {craft.Components[i].Describe()}.";
                    }
                await M.Say(msg);
                await vn.Wait(() => false);
            }, true, Cancellable.Replace(ref intuitionCt)), true);
        }
        
        protected override MapStateManager<PJ24IdealizedState, PJ24ADVData> ConfigureMapStates() {
            var m = Manager;
            var ms = new MapStateManager<PJ24IdealizedState, PJ24ADVData>(this, () => new(this));
            VNOperation cIn(SZYUCharacter x) => x.MoveBy(new(-1, 0, 0), 0.8f).And(x.FadeTo(1f, 0.8f));
            VNOperation cOut(SZYUCharacter x) => x.MoveBy(new(1, 0, 0), 0.8f).And(x.FadeTo(0f, 0.8f));
            C AddCustomer<C>(C x) where C: SZYUCharacter {
                vn.Add(x);
                x.LocalLocation.Value = new(4f, 0, 0);
                x.Alpha = 0;
                return x;
            }
            async Task<DMKVNState.RunningAudioTrackProxy> StartDialogueFade() {
                HideMD();
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1.2f));
                M.Alpha = 1;
                await M.SetEmote("");
                Md.LocalLocation.Value = new Vector3(0f, 0, 0);
                var result = vn.RunBGM("pj24.dialogue");
                await rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)).Task;
                ShowMD();
                return result;
            }
            async Task EndDialogueFade(DMKVNState.RunningAudioTrackProxy bgm) {
                HideMD();
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f));
                bgm.Dispose();
                M.Alpha = 0;
                _ = rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)).Task;
                ShowMD();
            }
            
            
            var sfail = Context("sfail", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("angry", "Asiram? Asiram? Get out here, Asiram!")).C,
                    M.ESayC("worry", "Huh? Chimata? Aren't you here early?"),
                    c.ESayC("surprise", "Early? What do you mean, <i>early</i>?"),
                    c.ESayC("angry", "You had a request due yesterday! You're late! <i>LATE</i>!"),
                    M.SayC("Ah, I got caught up in some stuff. I'll have it done today."),
                    c.SayC("That's not how this works, Asiram. My market can't function if the people who participate don't respect my time."),
                    c.ESayC("worry", "I hate to say this, but I have no choice other than to repo this atelier."),
                    M.ESayC("", "..."),
                    M.ESayC("worry", "Wait, what? Repro? Like a bug report?"),
                    c.ESayC("angry", "Repo as in repossess. I'm taking this atelier and I'll give it to someone who can respect my deadlines. You're homeless now. Get out."),
                    M.ESayC("angry", "What? Who gave you the right to my atelier? I own this place! You can't kick me out of here!"),
                    c.ESayC("worry", "Asiram, you need to learn to read the fine print. Even if you own this building, you signed a 99-year commercial lease for the land, which has some extra conditions not applied to homeowners."),
                    c.SayC("In particular, you are obligated to keep your shop open a certain number of days per year and serve a certain number of clients within rigidly-specified windows of error."),
                    c.SayC("According to my calculations, your one-day delay has caused over one BILLION dollars in losses for my market. Under the terms of your lease, I- as your landlord- can place a lien on your property for those losses."),
                    c.SayC("This property is worth less than one billion dollars, so it's being repoed. End of story."),
                    M.ESayC("cry", "What?! That doesn't make any sense!"),
                    c.EmoteSayC("", "I've given you the legal explanation, and I owe you nothing further."),
                    c.EmoteSayC("angry", "If you have any further questions, talk to your lawyer. But be quick. I want to see this place EMPTY by tomorrow morning. Got it?"),
                    M.SayC("I-"),
                    c.ESayC("happy", "Oh, and happy Pride Month!")
                );
                
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 2.5f));
                completion.SetResult(new UnitADVCompletion());
            });


            var s0_0 = Context("s0_0", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    M.ESayC("happy", "Would you look at that! It's May 29th!"),
                    M.ESayC("normal", "This is one of my favorite days of the year. After all, it's the approximately six hundredth anniversay of the Fall of Constantinople!"),
                    M.ESayC("happy", "I'm really looking forward to the meetup at the Historical Appreciation Society tonight!"),
                    cIn(c).And(c.EmoteSay("happy", "Hello! Is this Asiram's Alchemy Atelier?")).C,
                    M.ESayC("worry", "No, this is Marisa's Alchemy Atelier. I'm not aware of an Asiram..."),
                    c.ESayC("angry", "No, no. That name doesn't have the right initials. MAA? What is that supposed to be?"),
                    c.ESayC("happy", "Asiram's Alchemy Atelier - Triple-A - now that's a <i>good</i> name!"),
                    c.ESayC("", "Thus, under my authority as the God of Markets, I henceforth rename this shop to Asiram's Alchemy Atelier."),
                    M.SayC("Uh-huh... well, I don't really care either way, but... is there anything in particular you need? I'm planning to close up shop early today."),
                    c.ESayC("happy", "It's May 29th, my friend! Don't you know what that means?"),
                    M.ESayC("happy", "It's the anniversary of the Fall of Constantinople!"),
                    c.ESayC("worry", "Huh? What? No, I was talking about Pride Month."),
                    c.SayC("Pride Month starts in three days. And as it turns out, I don't have any Tritricolor Banners for my market."),
                    M.ESayC("", "..."),
                    M.ESayC("worry", "What's a Tritricolor Banner and why do you need it for Pride Month?"),
                    c.ESayC("", "You know, one of those rainbow flags with six colors."),
                    M.ESayC("", "Oh, I can craft you a rainbow flag."),
                    c.ESayC("angry", "No, no, you don't understand. You need to make me a Tritricolor Banner. It needs to be fancier than a normal rainbow flag."),
                    c.ESayC("", "And... yeah, it should be spun woven-hand, or something like that. You know how people love that retro stuff. Especially the pride people."),
                    M.ESayC("worry", "So... you want a Rainbow Flag with the Traditionally-Woven effect? I can get higher quality effects on my products, but I'll have to use more expensive materials."),
                    c.ESayC("", "It's a Tritricolor Banner. But yes. I want it made out of cotton or one of those hippie-dippie new-age fabrics, not plastic. And have it ready by May 31st."),
                    c.ESayC("", "Make sure to check the Requests window before you start making anything. It'll have more details about what exactly you need to provide."),
                    c.ESayC("happy", "Anyways, I have to go. I have to give a speech tonight at the Historical Appreciation Society about how my business acumen was inspired by the Tomatoan Empire taking down Constant Naples. You know how it is."),
                    c.SayC("See you soon!"),
                    cOut(c),
                    M.SayC("...Well, I think I understand what she wants me to do.")
                );
                await EndDialogueFade(bgm);
            });

            var s0_1 = Context("s0_1", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("", "Good morning! How's my commission coming along?")).C,
                    M.ESayC("", "Pretty good. I should have it ready shortly."),
                    c.SayC("Once you're finished, you can submit the required items by going to the Requests menu and clicking on my request. Then select the required items and click 'OK' to submit them."),
                    c.SayC("You'll immediately get the rewards from the request."),
                    M.ESayC("happy", "Makes sense. And after I've completed your request, I-"),
                    c.ESayC("happy", "Once you've finished all your requests, you can do whatever you want through the deadline."),
                    c.ESayC("", "The current deadline is 5/31, so you'll start getting new requests on 6/1."),
                    M.ESayC("", "Fair enough. And what if I don't feel like completing a request?"),
                    c.ESayC("happy", "That's a funny joke, Asiram. These requests are all sanctioned by me, the God of Markets. What do you think will happen to your little shop here if you don't complete them?"),
                    M.EmoteSayC("worry", "My name is Maris-"),
                    c.ESayC("", "Well, that's all from me. I have to go give a speech at the Women's Rights Convention about how Jean Dark inspired my revolutionary business ventures. Apparently today is her anniversary or something."),
                    c.ESayC("happy", "See you soon!"),
                    cOut(c)
                );
                await EndDialogueFade(bgm);
            });

            var s1_0 = Context("s1_0", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Asiram! The Tritricolor Banner you made was a real hit with the pride people!")).C,
                    M.ESayC("happy", "That's good to hear. Do you need any more rainbow flags? I can make more."),
                    M.ESayC("worry", "Oh, and to be clear, my name is Mari-"),
                    c.ESayC("", "No worries there. I got some vengeful spirits from Hell to make shoddy reproductions for cheap. The best part is, I don't even have to pay them!"),
                    c.ESayC("happy", "There is some stuff I do need from you, though. I need some, uh, rainbow stuff that I can put on other stuff. Oh, and it should glow in the dark."),
                    M.ESayC("", "So a Rainbow Dye and a Rainbow Paint Set... with some specific traits on them, right?"),
                    c.ESayC("", "That's right. Unlike effects, which are dependent on the quality of the ingredients you use, traits are directly inherited from the ingredients."),
                    c.ESayC("worry", "Ah, and one of the traits I need is Glowing 3... My understanding is that it's quite a rare trait."),
                    M.SayC("No worries. I'm an expert alchemist, so I can combine Glowing 1 and Glowing 2 to get Glowing 3."),
                    c.SayC("And I need Glowing 3 on both items. Do you have enough ingredients for that?"),
                    M.SayC("I don't, but if I craft two Rainbow Dyes simultaneously, then they'll share all the same traits. Then I can use one of the Rainbow Dyes to craft a Rainbow Paint Set."),
                    c.ESayC("happy", "That's great to know. In that case, I'll leave it in your hands. Make sure to get me the goods by the 14th of June."),
                    c.ESayC("happy", "Well, I have to go. Today's the annniversary of the founding of the ECB, so we're holding a conference on how banks can better exploit-- I mean, cater to the needs of-- the pride people. Happy Pride Month!"),
                    cOut(c)
                );
                await EndDialogueFade(bgm);
            });
            
            var s1_1 = Context("s1_1", async () => {
                using var c = AddCustomer(new Miko());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("", "Greetings. Is this Asiram's Alchemy Atelier?")).C,
                    M.EmoteSayC("worry", "No, this is Marisa's Alchemy Atelier."),
                    c.ESayC("worry", "Is that so? The sign outside says Asiram..."),
                    M.SayC("Chimata must have flipped the sign around..."),
                    M.ESayC("", "Well, that aside, if you need something crafted, I can do it for you."),
                    c.ESayC("happy", "I see. In fact, I do require your expertise, Marisa."),
                    c.ESayC("", "You see, there is one particularly beautiful black pegasus with whom I so love to ride."),
                    c.SayC("Ah, let me explain what I mean by beautiful, so that your hands may be well inspired."),
                    c.SayC("Her muscles are fluid yet incompressible, shapely yet unformed, as the water that cascades down Youkai Mountain..."),
                    c.SayC("Where her hooves strike there are gunshots, and the earth trembles in fear..."),
                    c.SayC("You cannot close your ears to the thunder or your eyes to the lightning! Thus is she, an unstoppable tempest rampaging across the sky..."),
                    c.ESayC("cry", "Being myself sickly by birth, I cannot help but find myself attracted to such an overwhelmingly physical beauty."),
                    c.ESayC("happy", "So I was thinking-"),
                    M.ESayC("worry", "Wait, I'm a bit lost. You have a horse?"),
                    c.ESayC("worry", "<i>Have</i> is not the correct verb, incapable as it is of reflecting the mutual nature of our relationship, but yes. Kurokoma Saki. I believe you've met her."),
                    M.ESayC("surprise", "Ohhhh... I see now. I was a bit confused for a moment."),
                    M.ESayC("happy", "So you want a present for your girlfriend?"),
                    c.ESayC("worry", "<i>Present</i> is not the correct term, per se. I was only considering procuring her some new banners for emblazonment during her horse races..."),
                    c.ESayC("cry", "But these light-scattering emblems are utterly lacking in stock now that it is June."),
                    M.ESayC("", "..."),
                    M.ESayC("worry", "What's a light-scattering emblem and what does it have to do with June?"),
                    c.ESayC("worry", "You must be aware of these banners painted in the colors of the rainbow, no?"),
                    M.ESayC("happy", "Oh, a Tritricolor Banner! I thought Chimata was making more of those, but if you can't find any, I'll make one for you."),
                    c.ESayC("happy", "How kind of you. I expect much of your work."),
                    c.ESayC("", "Then, I shall excuse myself. I have an illegal horse race I must go place bets upon."),
                    cOut(c)
                );
                UpdateDataV(d => d.Phases[1].Requests[2].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s1_2 = Context("s1_2", async () => {
                using var c = AddCustomer(new Kaguya());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("cry", "Marisa? Is this Marisa's place?")).C,
                    M.EmoteSayC("worry", "Kaguya? Are you all right?"),
                    c.SayC("Ah, Marisa. Why do you have to open your store so late at night? These hours are so inconvenient..."),
                    M.SayC("...It's 7 in the morning right now. Don't all alchemy ateliers open around dawn?"),
                    c.ESayC("surprise", "7:00 AM?! You would ask your customers to stay up until 7:00 AM?! Ah, how dreadful..."),
                    c.ESayC("cry", "Well, whatever. Let me make my order, and then I'll go to sleep..."),
                    c.ESayC("", "The weather has gotten quite nice lately, so Mokou and I have gotten back into our nightly deathmatches."),
                    M.ESayC("surprise", "Deathmatch? How can you have a deathmatch when neither of you can die?"),
                    c.ESayC("happy", "Many little deaths, as they say."),
                    c.ESayC("worry", "Problem is, it gets quite dark at night, so we sometimes can't see each others' faces, no matter how close we get..."),
                    c.ESayC("happy", "And so I was thinking that it would be a good idea to procure a nice oil lamp for setting the mood."),
                    M.ESayC("", "I can make you an Oil Lamp. Any special effects you'd like on it?"),
                    c.ESayC("", "Well, I suppose it should be sturdy... and flexible... and the flame should flow efficiently."),
                    M.ESayC("worry", "Wait, what? How does that work?"),
                    c.ESayC("worry", "I don't know, I'm not the alchemist here."),
                    c.ESayC("cry", "Ah, it's so late... I'm going to go sleep."),
                    cOut(c)
                );
                UpdateDataV(d => d.Phases[1].Requests[3].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s1_3 = Context("s1_3", async () => {
                using var c = AddCustomer(new Yukari());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    VN.SFX("vn-yukari-power"),
                    cIn(c).And(c.EmoteSay("happy", "Marisa! Marisa, are you here?")).C,
                    M.EmoteSayC("", "G'morning, Yukari. Did you need something crafted?"),
                    c.ESayC("", "That's right. As it turns out, we need a new harness for my wife's dragon."),
                    M.ESayC("worry", "...You mean literally, or is that a euphemism?"),
                    c.ESayC("worry", "Huh? What? No, it's an actual dragon. You know, like Qinglong. Though it's born of the lightning essence, so I suppose it's more like Huanglong."),
                    M.SayC("Huanglong is associated with the earth element."),
                    c.ESayC("", "If you say so."),
                    c.SayC("Anyways, you can do that, right? Shouldn't be too hard for a young, enterprising alchemist."),
                    M.ESayC("", "Yup, if it's an actual dragon harness that you want, and I'm not missing out on some kind of double entendre, then I can definitely do that for you."),
                    c.ESayC("happy", "Sounds great. I'll be expecting much from you."),
                    VN.SFX("vn-yukari-power"),
                    cOut(c)
                );
                UpdateDataV(d => d.Phases[1].Requests[4].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s2_0 = Context("s2_0", async () => {
                using var c = AddCustomer(new Koishi());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Hello.")).C,
                    M.EmoteSayC("", "Good morning."),
                    c.ESayC("", "..."),
                    M.ESayC("", "..."),
                    c.SayC("..."),
                    M.ESayC("worry", "..."),
                    c.SayC("..."),
                    M.ESayC("worry", "Do you, uh, need something?"),
                    c.ESayC("happy", "Yes."),
                    c.ESayC("", "I am looking for some... Eye Bleach."),
                    M.SayC("You mean, like eye drops?"),
                    c.SayC("No, eye bleach."),
                    M.SayC("..."),
                    c.SayC("..."),
                    M.SayC("..."),
                    c.SayC("..."),
                    M.SayC("Why?"),
                    c.ESayC("cry", "My sister, Satori, has been too obsessed with her work lately."),
                    c.ESayC("worry", "I was thinking that it's about time she goes out and makes a few friends."),
                    c.SayC("However, she won't listen to my recommendations, and keeps insisting that she needs to focus on her career."),
                    M.SayC("..."),
                    c.ESayC("", "..."),
                    M.SayC("..."),
                    c.SayC("..."),
                    M.SayC("And... how does eye bleach come into the picture?"),
                    c.SayC("I'm pretty sure that she's obsessed with her work precisely because of her mind-reading abilities."),
                    c.ESayC("happy", "So if I use eye bleach to give her third eye conjunctivis, then she might be able to stop thinking about work."),
                    c.ESayC("", "Of course, it will only be temporary. She is a youkai, so a human poison like eye bleach cannot harm her much or for long."),
                    M.SayC("I see... but there are clear safety concerns here. Bleach is fairly dangerous, and even if I synthesize it in a way that maximizes safety, I can't guarantee-"),
                    c.SayC("If you complete this commission, I will give you two Soul Shells."),
                    M.ESayC("surprise", "Soul Shells?! Those things are <i>crazy</i> expensive!"),
                    c.SayC("Yes. I have learned that expensive things tend to assuage people's doubts."),
                    M.ESayC("happy", "Well, I suppose that you know your sister best. I'll do what I can to keep it safe, but rest assured, I'll have this done in a jiffy!"),
                    c.ESayC("happy", "Many thanks, Asiram."),
                    cOut(c).And(M.EmoteSay("surprise", "Wait! My name isn't Asiram!")).C
                );
                //UpdateDataV(d => d.Phases[2].Requests[1].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s2_1 = Context("s2_1", async () => {
                using var c = AddCustomer(new Yukari());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    VN.SFX("vn-yukari-power"),
                    cIn(c).And(c.EmoteSay("cry", "Marisa...")).C,
                    M.EmoteSayC("worry", "Yukari? What's got you down?"),
                    c.ESayC("surprise", "The dragon harness broke!"),
                    M.ESayC("surprise", "Really?! How rough were you at it?!"),
                    c.ESayC("cry", "I think my wife's dragon is too powerful for a normal harness."),
                    c.ESayC("worry", "Can you make a super-strong Dragon Harness?"),
                    M.ESayC("worry", "Hmm... I suppose I could try crafting a Dragon Harness with the Antifragile effect."),
                    M.ESayC("", "Oh yeah- you did actually give me some Dragon Scales last time. I could use those to make Dragonscale Cloth, which is stronger than normal cloth."),
                    c.ESayC("happy", "Really? That would be great!"),
                    c.ESayC("", "A harness made of dragon scales should be able to withstand a dragon's force."),
                    c.ESayC("happy", "This will definitely work! Second time's the charm!"),
                    VN.SFX("vn-yukari-power"),
                    cOut(c),
                    M.ESayC("worry", "She definitely jinxed it...")
                );
                UpdateDataV(d => d.Phases[2].Requests[1].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s2_2 = Context("s2_2", async () => {
                using var c = AddCustomer(new Seiga());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Good morning~")).C,
                    M.ESayC("angry", "Seiga? God, I don't even want to know what you're going to ask for."),
                    c.SayC("Come, come, don't say such rude things. I'm just looking for some standard Bone Hurting Juice."),
                    M.SayC("Steroids are classified as Schedule III under the Controlled Substances Act. I don't deal in-"),
                    M.ESayC("worry", "Wait, did you say Bone <i>Hurting</i> Juice? Not Bone <i>Building</i> Juice?"),
                    c.ESayC("", "That's right. And Bone Hurting Juice is a perfectly legal supplement that can be procured OTC."),
                    c.ESayC("cry", "You see, my dearest Yoshika is having trouble with her arthritis lately..."),
                    M.ESayC("angry", "Isn't that because you made her a jiangshi?"),
                    c.ESayC("happy", "Exactly, and that's also why I have to resolve this conundrum, wouldn't you say?"),
                    c.ESayC("", "It's well-understood in the medical community that Bone Hurting Juice helps loosen stiff joints. So this is a perfectly justified usage of it. Right?"),
                    M.ESayC("worry", "...Are you really not planning to use it for more nefarious purposes, like mixing it into someone's drink?"),
                    c.ESayC("surprise", "What? Of course not! I, Kaku Seiga, the embodiment of Lawful Good, would never do such a thing."),
                    M.ESayC("angry", "..."),
                    c.ESayC("happy", "So, thanks in advance!"),
                    cOut(c)
                );
                UpdateDataV(d => d.Phases[2].Requests[2].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s2_3 = Context("s2_3", async () => {
                using var c = AddCustomer(new Koishi());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Hello.")).C,
                    M.EmoteSayC("happy", "Oh, Koishi. You're back. How did the thing with the eye bleach go?"),
                    c.ESayC("happy", "Spectacularly!"),
                    c.ESayC("", "Once my sister's third eye got all puffy, she said that she was finally able to see what was really most important to her all this time..."),
                    M.ESayC("", "..."),
                    c.SayC("..."),
                    M.ESayC("worry", "..."),
                    c.SayC("..."),
                    M.SayC("That being?"),
                    c.ESayC("happy", "Someone named Miyadeguchi Mizuchi. I think that's her lover!"),
                    M.ESayC("surprise", "Miyadeguchi Mizuchi...?!"),
                    M.ESayC("", "I don't really recall the name. Must be someone from Hell."),
                    c.ESayC("happy", "Yeah, and apparently my sister wants to gift her a Soul Chain! As a token of love or something!"),
                    M.ESayC("worry", "A Soul Chain? Aren't those pretty dangerous? I think they're used for imprisoning wandering souls within mortal shells."),
                    c.ESayC("", "I don't really know, but I've heard that danger breeds romance."),
                    M.ESayC("", "Ah... I see..."),
                    M.ESayC("happy", "Well, I do owe Satori some favors, so I'll craft a Soul Chain for her."),
                    c.ESayC("happy", "I knew I could count on you, Asiram!"),
                    c.ESayC("", "Let's work together to make my sister's romance a burning success!"),
                    M.SayC("Yeah! Let's!"),
                    M.ESayC("surprise", "Wait, did you just call me Asiram?"),
                    cOut(c).And(M.EmoteSay("angry", "Stop! Wait! My name isn't Asiram!")).C
                );
                UpdateDataV(d => d.Phases[2].Requests[3].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s3_0 = Context("s3_0", async () => {
                using var c = AddCustomer(new Chimata());
                using var r = AddCustomer(new Reimu());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    M.ESayC("happy", "Ah, July 1st. The anniversary of the Great Railroad Strike of 1922!"),
                    M.ESayC("", "I wonder if my calendar has pictures of trains this month. Let's see..."),
                    M.ESayC("worry", "Wait, what? Why does my calendar say June 31st? I'm pretty sure June has only 30 days...?"),
                    cIn(c).And(c.EmoteSay("happy", "Good morning, Asiram!")).C,
                    M.ESayC("angry", "Chimata? For the hundredth time, my name isn't Asiram!"),
                    c.ESayC("", "At least until the end of Pride Month, it is."),
                    M.ESayC("worry", "What? Didn't Pride Month end yesterday?"),
                    c.ESayC("worry", "Oh, you didn't get the memo?"),
                    c.SayC("Pride Month this year was so profitable- excuse me, I mean <i>popular</i>- that we decided to extend it by 13 days."),
                    M.SayC("...You can... extend June?"),
                    c.ESayC("", "I own controlling shares in all the timekeeping companies, so <i>I</i> can, yes."),
                    M.SayC("I... but... the moon..."),
                    c.SayC("Luckily, I recently won a sweatshop staffed by animal spirits in an illegal horse race, so I don't need any more dyes or paints from you. Your products are too expensive for mass production, as it stands."),
                    c.ESayC("happy", "I just came to let you know that I'm doing some advanced focus testing on the next big fad, and I should have an order in for you in about a week."),
                    c.SayC("You should save your game now if you haven't already, because if you screw up, you won't be able to complete my super advanced request."),
                    c.ESayC("", "Keep your schedule open, Asiram!"),
                    cOut(c).And(M.EmoteSay("cry", "But... you can't just change the calendar...")).C,
                    M.SayC("...Well, I suppose it doesn't really matter. It's not like my work here changes either way."),
                    M.ESayC("", "Just think of it as a normal day, and take customers' orders like normal."),
                    cIn(r).And(r.EmoteSay("", "Yo.")).C,
                    M.ESayC("happy", "Reimu! What's up?"),
                    r.SayC("Hey Marisa. I actually wanted you to craft something for me."),
                    r.SayC("I need a Refractive Magic Fuel, and it needs to have the trait Illuminating."),
                    M.ESayC("", "Sure, I can do that."),
                    r.SayC("Ah, and also, it needs to have the effect Love-Colored. As in Love-Colored Master Spark."),
                    M.ESayC("happy", "Only the highest quality here, of course."),
                    r.SayC("Aight, thanks. Imma dip then."),
                    M.ESayC("worry", "...Wait a second."),
                    r.ESayC("surprise", "Huh?! What's the problem?"),
                    M.SayC("Refractive Magic Fuel is only useful for magic tools like my Hakkero. I'm pretty sure nobody else around here uses it."),
                    M.SayC("What exactly are you planning to use it for? Just so you know, it doesn't combust like normal fuel, so you can't use it for fireworks or anything like that."),
                    r.ESayC("worry", "W-what? What does it matter to you what use I have for what I'm buying with my own money?!"),
                    M.ESayC("", "My job here is to do my best to satisfy your needs. I can't do that unless I know what you're trying to achieve."),
                    r.ESayC("emb1", "...Marisa..."),
                    M.ESayC("happy", "That's what it means to be a small business owner!"),
                    r.ESayC("angry", "..."),
                    r.SayC("Well, what I am TRYING to ACHIEVE is POSSESSING a REFRACTIVE MAGIC FUEL. So hurry up and make one for me!"),
                    cOut(r).And(r.Say("You better do it!")).C,
                    M.ESayC("worry", "...Did I say something wrong there?"),
                    M.ESayC("surprise", "...Wait, I forgot! I'm out of Scattering Opals! I can't craft Refractive Magic Fuel without Scattering Opals!"),
                    M.ESayC("worry", "For now, I'll just have to craft the rest of the ingredients, and make sure to get the right traits. I'll need some high-quality oil if I want to get the Love-Colored effect...")
                );
                //UpdateDataV(d => d.Phases[3].Requests[0].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s3_1 = Context("s3_1", async () => {
                using var c = AddCustomer(new Kasen());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("", "Marisa.")).C,
                    M.ESayC("happy", "Hey, Kasen. It's been a while."),
                    c.SayC("The both of us haven't been at the shrine much recently. I suspect Reimu is a bit lonely without us."),
                    M.ESayC("worry", "Aye, that makes sense. I should probably visit her soon."),
                    c.ESayC("worry", "That aside, there's something I wanted to ask you about..."),
                    c.ESayC("", "You know Koutei, my pet lightning dragon, right?"),
                    c.SayC("Yukari and I have been looking around for a nice harness for him, but... we haven't been able to find anything good."),
                    M.ESayC("surprise", "Oh, so the harness actually WAS for a dragon?"),
                    c.ESayC("worry", "I'm... not sure what else it could be for."),
                    c.ESayC("", "Anyways, the harness you made for Yukari last time was pretty good, but the problem is that it can't hold up under Koutei's lightning aura."),
                    c.ESayC("happy", "Could you make two of those, except also with the Resists Lightning trait?"),
                    c.ESayC("", "With that, I think Yukari and I will finally be able to go dragonriding together."),
                    M.ESayC("happy", "Of course. Easy work for an alchemist such as myself."),
                    c.ESayC("happy", "Great. I'll leave it in your hands, then."),
                    cOut(c),
                    M.ESayC("worry", "Wait a second. I don't have any ingredients with Resists Lightning!"),
                    M.ESayC("cry", "Reimu said she'd reward me with something that has Resists Lightning, but I still don't have any Scattering Opals...")
                );
                UpdateDataV(d => d.Phases[3].Requests[1].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s3_2 = Context("s3_2", async () => {
                using var c = AddCustomer(new Mizuchi());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("", "Asiram? I'm looking for an Asiram.")).C,
                    M.ESayC("worry", "My name is Marisa, not Asiram."),
                    c.ESayC("", "Ah, sure. You're the local alchemist, right?"),
                    M.ESayC("happy", "That's right. If you need something crafted, I can..."),
                    M.ESayC("surprise", "Hey, wait! I've seen your face before! You're-"),
                    c.ESayC("smug", "That's right. I'm Miyadeguchi Mizuchi. Gensoukyou's number one criminal!"),
                    M.ESayC("angry", "You better not be trying to commit crimes in MY atelier!"),
                    c.ESayC("", "Nah. I mean, I WAS planning on a few crimes today, but then I found out that it's somehow still Pride Month, so I decided to delay my plans."),
                    M.ESayC("worry", "...What does Pride Month have to do with committing crimes?"),
                    c.ESayC("worry", "Well, obviously, you're only supposed to commit gay crimes during Pride Month. But I already used up all of my gay crimes earlier in June."),
                    M.ESayC("angry", "I have no idea what that's supposed to mean. Let me just call Reimu so she can exterminate you."),
                    c.ESayC("smug", "Can you really afford to do that, though?"),
                    M.ESayC("surprise", "What?! Are you threatening me?!"),
                    c.SayC("It's not a threat. I just happen to know the situation you're in."),
                    c.ESayC("", "Without a Scattering Opal, you can't craft a Refractive Magic Fuel for Reimu."),
                    c.SayC("And without the Resists Lightning trait that Reimu will pay you with, you can't craft a Dragon Harness for Kasen."),
                    c.ESayC("smug", "And without the Dragon Horn from Kasen, you can't beat the final boss of Pride Month."),
                    M.ESayC("worry", "Huh? There's a final boss of Pride Month?"),
                    c.ESayC("", "Of course there is. And just to be clear, it ain't some petty criminal like me."),
                    c.ESayC("", "I can get you TWO Scattering Opals. And all I need from you... is a Soul Guard."),
                    M.ESayC("surprise", "A Soul Guard? Aren't those for negating the binding effects of Soul Chains..."),
                    M.ESayC("angry", "...like the Soul Chain I made for Satori?"),
                    c.ESayC("happy", "That's right. I don't want to get caught by her again."),
                    M.SayC("There's no way I'm going to help a criminal escape justice!"),
                    c.ESayC("smug", "Even if it means you can't finish any of your commissions as an alchemist?"),
                    M.ESayC("worry", "I..."),
                    c.ESayC("smug", "Plus, even if I do go free, it's not <i>your</i> problem. It's Satori's problem. In other words, it's a negative externality."),
                    c.ESayC("", "You're running a business. Worrying about negative externalities is a breach of your fiduciary duty to shareholder value."),
                    c.ESayC("surprise", "You know what happened to the last guy who suggested that it maybe wasn't such a good idea to dump battery acid in local rivers? He got ousted as CEO."),
                    M.SayC("Wait, are you telling me that the market incentivizes me to ignore the effects of my actions on society when they don't directly feed back into my own balance sheet?"),
                    c.ESayC("", "Yes."),
                    c.ESayC("happy", "So good luck!"),
                    cOut(c),
                    M.SayC("Damn... I don't want to help her, but I really need those Scattering Opals!")
                );
                UpdateDataV(d => d.Phases[3].Requests[2].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var s3_3 = Context("s3_3", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Asiram, the focus group results are in!")).C,
                    M.ESayC("worry", "My name's not- Actually, forget it."),
                    c.ESayC("", "Rainbows are out. The new fad is millenial gray!"),
                    c.ESayC("happy", "So I want you to craft a Monochrome Dragon Statue for me."),
                    M.SayC("Monochrome? During Pride Month?"),
                    c.ESayC("worry", "Well, obviously, I won't be selling this during Pride Month. It's for the rest of the year, when people return to thinking of rainbows as tacky and oversaturated."),
                    c.ESayC("", "Ah, and I do need some special traits on it, so my sweatshops can use some advanced new machinery to accurately reproduce it for cheap."),
                    M.ESayC("angry", "I don't like the sound of that. If you're reproducing customized goods that I make, isn't that copyright infringement?"),
                    c.ESayC("happy", "That's a funny joke, Asiram. But copyright infringement is only a crime if you don't have an expensive lawyer."),
                    c.ESayC("", "And remember- you are <i>obligated</i> to fulfill my requests. So get to it! You have one week."),        
                    cOut(c).And(c.EmoteSay("happy", "Happy end-of-Pride Month!")).C
                );
                UpdateDataV(d => d.Phases[3].Requests[3].Visible = true);
                await EndDialogueFade(bgm);
            });
            
            var sgoodend = Context("sgoodend", async () => {
                using var c = AddCustomer(new Chimata());
                var bgm = await StartDialogueFade();
                await vn.Sequential(
                    cIn(c).And(c.EmoteSay("happy", "Asiram, my favorite alchemist!")).C,
                    M.ESayC("surprise", "Chimata? Don't tell me- Pride Month got extended again?"),
                    c.ESayC("surprise", "Oh, no, of course not."),
                    c.ESayC("", "Rather, the Monochrome Dragon Statue you made is really quite popular. Pride Month isn't even over yet, and people are already buying the cheap plastic reproductions in droves."),
                    c.ESayC("happy", "We've had a great partnership this past month. And I've made a ton of money!"),
                    M.ESayC("", "A ton of money?"),
                    c.ESayC("", "A ton."),
                    M.SayC("..."),
                    c.SayC("..."),
                    M.ESayC("worry", "..."),
                    c.SayC("..."),
                    M.SayC("Are you going to give me any of that money? Since I made all the stuff for you."),
                    c.ESayC("happy", "That's a funny joke, Asiram. But there's no royalty clause in our contract, so you're not getting anything."),
                    M.ESayC("worry", "Is... that how it works?"),
                    c.ESayC("happy", "Yes."),
                    M.ESayC("angry", "I suppose I'll need to get a royalty clause in our next contract, then."),
                    c.ESayC("", "Well, good luck negotiating that with my lawyers. Money is like hot air- it always flows upwards."),
                    c.ESayC("happy", "Upwards to me! The God of Markets!"),
                    c.ESayC("", "Anyways, that's all I wanted to tell you. I have an appointment to buy a new yacht with all the money I made off your labor, so I have to head out soon."),
                    c.ESayC("happy", "Oh, and happy Pride Month!")
                );
                
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 2.5f));
                completion.SetResult(new UnitADVCompletion());
            });

            void AssertDelayed(PJ24IdealizedState i, BoundedContext<Unit> bctx) {
                //VN assertion may occur on synth success or request submit menu;
                // in these cases, instead of running the VN immediately, it's nicer UX to 
                // wait for the user to return to the main menu, which will clear the existing intuition
                // and run TryRunBaseMenuIntuition, which TryOrDelayExecute has priority over.
                i.Assert(new RunOnEntryAssertion(() => {
                    Manager.TryOrDelayExecuteVN(bctx);
                }) {
                    ID = bctx.ID,
                    Priority = (int.MaxValue, 0)
                });
            }
            
            ms.ConfigureMap(House, (i, d) => {
                i.Assert(new EntityAssertion<MarisaRoomBG>(VN));
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = V3(-3f, 0),
                    Tint = new FColor(1,1,1,0)
                });
                if (!s0_0.IsCompletedInContexts())
                    i.SetEntryVN(s0_0);
                if (i.HasEntryVN || Manager.NonWaitingState >= ADVManager.State.Dialogue) goto bgm;
                if (d.Date.Cmp(d.Phase.Deadline) > 0) {
                    if (!d.Phase.PhaseComplete) {
                        AssertDelayed(i, sfail);
                        goto bgm;
                    } else ++d.PhaseIndex;
                }
                if (d.Date.Cmp(new Date(5, 30)) >= 0 && !s0_1.IsCompletedInContexts())
                    AssertDelayed(i, s0_1);
                else if (d.Date.Cmp(new Date(6, 1)) >= 0 && !s1_0.IsCompletedInContexts())
                    AssertDelayed(i, s1_0);
                else if (d.Date.Cmp(new Date(6, 2)) >= 0 && !s1_1.IsCompletedInContexts())
                    AssertDelayed(i, s1_1);
                else if (d.Date.Cmp(new Date(6, 4)) >= 0 && !s1_2.IsCompletedInContexts())
                    AssertDelayed(i, s1_2);
                else if (d.Date.Cmp(new Date(6, 6)) >= 0 && !s1_3.IsCompletedInContexts())
                    AssertDelayed(i, s1_3);
                else if (d.Date.Cmp(new Date(6, 14)) >= 0 && !s2_0.IsCompletedInContexts())
                    AssertDelayed(i, s2_0);
                else if (d.Phases[2].Requests[0].Complete && !s2_3.IsCompletedInContexts())
                    AssertDelayed(i, s2_3);
                else if (d.Date.Cmp(new Date(6, 15)) >= 0 && !s2_1.IsCompletedInContexts())
                    AssertDelayed(i, s2_1);
                else if (d.Date.Cmp(new Date(6, 20)) >= 0 && !s2_2.IsCompletedInContexts())
                    AssertDelayed(i, s2_2);
                else if (d.Date.Cmp(new Date(6, 31)) >= 0 && !s3_0.IsCompletedInContexts())
                    AssertDelayed(i, s3_0);
                else if (d.Date.Cmp(new Date(6, 32)) >= 0 && !s3_1.IsCompletedInContexts())
                    AssertDelayed(i, s3_1);
                else if (d.Date.Cmp(new Date(6, 33)) >= 0 && !s3_2.IsCompletedInContexts())
                    AssertDelayed(i, s3_2);
                else if (d.Date.Cmp(new Date(6, 36)) >= 0 && !s3_3.IsCompletedInContexts())
                    AssertDelayed(i, s3_3);
                else if (d.PhaseIndex == 3 && d.Phase.PhaseComplete)
                    AssertDelayed(i, sgoodend);
                bgm:
                i.Assert(new BGMAssertion(VN, d.PhaseIndex switch {
                    2 => "pj24.june2",
                    3 => "pj24.june3",
                    _ => "pj24.june1"
                }));
            });
            return ms;
        }
        
        public record PJ24IdealizedState(Executing e) : ADVIdealizedState(e) {
            protected override Task FadeIn(ActualizeOptions options) {
                if (options.ActualizeFromNull) {
                    e.rg.Visible.Value = true;
                    return Task.CompletedTask;
                }
                return e.rgb.DoTransition(new RenderGroupTransition.Fade(e.rg, 0.7f)).Task;
            }
            protected override Task FadeOut(ActualizeOptions options) {
                return e.rg.DoTransition(new RenderGroupTransition.Fade(e.rgb, 0.7f)).Task;
            }
        }
    }

    public abstract class GamePhase {
        public abstract string Title { get; }
        public abstract Date Deadline { get; }
        public abstract Request[] Requests { get; init; }
        protected abstract int[] PreferredOrder { get; }
        [JsonIgnore] public Request? RecommendedNext {
            get {
                foreach (var idx in PreferredOrder)
                    if (Requests[idx].Visible && !Requests[idx].Complete)
                        return Requests[idx];
                return null;
            }
        }
        [JsonIgnore] public bool PhaseComplete {
            get {
                for (int ii = 0; ii < Requests.Length; ++ii)
                    if (!Requests[ii].Complete)
                        return false;
                return true;
            }
        }
        [JsonIgnore] public bool RemainingRequestsUncollected {
            get {
                if (PhaseComplete) return false;
                return RecommendedNext is null;
            }
        }

        public class May : GamePhase {
            public override string Title => "5月下旬";
            public override Date Deadline => new(5, 31);
            public override Request[] Requests { get; init; } = {
                new("Chimata", new(TritricolorBanner.S, new(){new(TraditionallyWoven.S)}), 1, new[] {
                    (new ItemInstance(BagOfNuts.S), 8),
                    (new ItemInstance(RainbowRose.S, traits:new(){new(Sturdy.S)}), 2),
                    (new ItemInstance(RainbowRose.S, traits:new(){new(Glowing2.S)}), 1),
                    (new ItemInstance(RainbowRose.S), 5),
                    (new ItemInstance(Water.S, traits:new(){new(Glowing1.S)}), 1),
                    (new ItemInstance(Water.S), 15),
                }, "Chimata asked for a high-quality \"tritricolor\" flag. I've never heard of the rainbow flag" +
                   " being called that before...") {
                    Visible = true
                }
            };
            
            protected override int[] PreferredOrder { get; } = { 0 };
        }
        public class June1 : GamePhase {
            public override string Title => "6月上旬";
            public override Date Deadline => new(6, 13);
            public override Request[] Requests { get; init;  } = {
                new("Chimata", new(RainbowDye.S, traits:new(){new(Glowing3.S)}), 1, new[] {
                    (new ItemInstance(Water.S, traits:new(){new(EfficientFlow.S)}), 2),
                    (new ItemInstance(PetrochemicalWaste.S), 6),
                }, "Chimata asked for some glowing Rainbow Dye. Why does it need to be glowing, though...?") {
                    Visible = true
                },
                new("Chimata", new(RainbowPaintSet.S, traits: new(){new(Sturdy.S),new(EfficientFlow.S),new(Glowing3.S)}), 1, new[] {
                        (new ItemInstance(Ingot.S, traits:new(){new(ResistsFire.S)}), 2),
                        (new ItemInstance(Ingot.S), 4),
                        (new ItemInstance(PetrochemicalWaste.S), 6),
                    }, "Chimata asked for a particularly complex Rainbow Paint Set. Normally she's a cheapskate, but I'm going to charge her a lot for this.") {
                    Visible = true
                },
                new("Miko", new(TritricolorBanner.S), 1, new[] {
                    (new ItemInstance(Alcohol.S), 4),
                    (new ItemInstance(Water.S), 8),
                    (new ItemInstance(PetrochemicalWaste.S, traits:new(){new(Flexible.S)}), 2),
                }, "Miko asked for a rainbow flag. It's definitely going to get ripped up during Saki's horse races, so I might as well make her a cheap one."),
                new("Kaguya", new(OilLamp.S, traits: new(){new(Sturdy.S),new(Flexible.S),new(EfficientFlow.S), new(ResistsFire.S)}), 1, new[] {
                        (new ItemInstance(FlaxFiber.S), 10),
                    }, "Kaguya asked for an oil lamp. I should make sure it won't explode if it gets hit by one of Mokou's stray fireballs."),
                new("Yukari", new(DragonHarness.S), 1, new[] {
                    (new ItemInstance(DragonScale.S), 6),
                    (new ItemInstance(Salt.S), 8),
                    (new ItemInstance(Red40.S, traits:new(){new(TastesLikeCoke.S)}), 1),
                    (new ItemInstance(Red40.S), 3),
                }, "Yukari wants me to craft a Dragon Harness. I suppose you can't get those from Outside anymore."),
            };
            
            protected override int[] PreferredOrder { get; } = { 2, 0, 1, 3, 4 };
        }
        public class June2 : GamePhase {
            public override string Title => "6月中旬";
            public override Date Deadline => new(6, 30);
            public override Request[] Requests { get; init; } = {
                new("Koishi", new(EyeBleach.S, traits:new(){new(SweetAndSour.S)}), 1, new[] {
                    (new ItemInstance(SoulShell.S), 2),
                    (new ItemInstance(RawMeat.S, traits:new(){new(Rotten.S)}), 1),
                    (new ItemInstance(RawMeat.S), 3),
                }, "I'm not sure it's such a great idea to dump bleach on Satori's eye, but Koishi is promising me" +
                   " Soul Shells, which are pretty valuable...") {
                    Visible = true
                },
                new("Yukari", new(DragonHarness.S, new(){new(Antifragile.S)}), 1, new[] {
                        (new ItemInstance(Blackberry.S, traits: new(){new(Sweet.S)}), 1),
                        (new ItemInstance(Blackberry.S, traits: new(){new(Sour.S)}), 1),
                        (new ItemInstance(Blackberry.S), 2),
                    }, "Apparently a normal Dragon Harness isn't good enough for Yukari's dragon. I'll have to make one" +
                       " with the Antifragile trait."),
                new("Seiga", new(BoneHurtingJuice.S, traits: new(){new(Rotten.S), new(TastesLikeCoke.S)}), 1, new[] {
                        (new ItemInstance(FlaxFiber.S), 2),
                        (new ItemInstance(Water.S), 8),
                    }, "Normally I wouldn't craft something as dangerous as Bone Hurting Juice, but I <i>am</i>" +
                       " kind of curious about what rotten meat and coke taste like together."),
                new("Koishi", new(SoulChain.S, traits: new(){new(ResistsFire.S)}), 1, new[] {
                        (new ItemInstance(CrystallizedFantasy.S, traits:new(){new(Illuminating.S)}), 2),
                    }, "It's too bad that Satori is so obsessed with her job, but I <i>should</i> help her if I can."),
            };
            
            protected override int[] PreferredOrder { get; } = { 2, 1, 0, 3 };
        }
        public class June3 : GamePhase {
            public override string Title => "6月下旬";
            public override Date Deadline => new(6, 43);
            public override Request[] Requests { get; init; } = {
                new("Reimu", new(RefractiveMagicFuel.S, new(){new(LoveColor.S)}, traits:new(){new(Illuminating.S)}), 1, new[] {
                        (new ItemInstance(PetrochemicalWaste.S, traits:new(){new(ResistsLightning.S)}), 1),
                    }, "Reimu demanded that I make some Refractive Magic Fuel for her... but I'm the only person around here who uses that.") {
                    Visible = true
                },
                new("Kasen", new(DragonHarness.S, new(){new(Antifragile.S)}, traits:new(){new(ResistsLightning.S)}), 2, new[] {
                    (new ItemInstance(DragonHorn.S), 1),
                }, "I have to try crafting that Dragon Harness one more time, this time with the Resists Lightning trait."),
                new("Mizuchi", new(SoulGuard.S, traits:new(){new(ResistsFire.S)}), 1, new[] {
                    (new ItemInstance(ScatteringOpal.S), 2),
                }, "I really shouldn't be making tools for a criminal, but... Scattering Opals are really shiny!"),
                new("Chimata", new(GrayDragonStatue.S, traits:new(){new(ResistsLightning.S), new(Illuminating.S)}), 1, new[] {
                    (new ItemInstance(Water.S), 1),
                }, "Apparently millenial gray is the new 'in' style... so long, rainbows!"),
            };
            
            protected override int[] PreferredOrder { get; } = { 2, 0, 1, 3 };
        }
    }

    [Serializable]
    public record PJ24ADVData: ADVData {
        public Date Date { get; set; } = new(5, 29);
        public GamePhase[] Phases { get; init; } = new GamePhase[] {
            new GamePhase.May(),
            new GamePhase.June1(),
            new GamePhase.June2(),
            new GamePhase.June3()
        };
        public int PhaseIndex { get; set; } = 0;
        [JsonIgnore] public GamePhase Phase => Phases[PhaseIndex];
        
        [JsonConverter(typeof(ComplexDictKeyConverter<Item, List<ItemInstance>>))]
        public Dictionary<Item, List<ItemInstance>> Inventory { get; init; } = new() {
            {MoldablePlastic.S, new() {new(MoldablePlastic.S)}},
            {LinenCloth.S, new() {new(LinenCloth.S)}},
            {RainbowDye.S, new() {new(RainbowDye.S),new(RainbowDye.S)}},
            {MagnoliaBloom.S, new() {new(MagnoliaBloom.S),new(MagnoliaBloom.S)}},
        };
        [JsonIgnore] public Event<ItemInstance> ItemAdded { get; } = new();

        public PJ24ADVData(InstanceData VNData) : base(VNData) {
        }

        public void SubmitRequest(RequestSubmit submission) {
            foreach (var item in submission.Selected)
                RemoveItem(item);
            foreach (var (item, ct) in submission.Req.Reward)
                for (int ii = 0; ii < ct; ++ii)
                    AddItem(item.Copy());
            submission.Req.Complete = true;
        }
        
        public ItemInstance ExecuteSynthesis(CurrentSynth synth) {
            Date += synth.Recipe.DaysTaken(synth.Count);
            foreach (var item in synth.Selected.SelectMany(x => x))
                RemoveItem(item);
            var result = synth.Recipe.Synthesize(synth.Selected);
            AddItem(result);
            for (int ii = 1; ii < synth.Count; ++ii)
                AddItem(result.Copy());
            return result;
        }

        public void AddItem(ItemInstance item) {
            Inventory.AddToList(item.Type, item);
            ItemAdded.OnNext(item);
        }

        public void RemoveItem(ItemInstance item) {
            Inventory[item.Type].Remove(item);
            item.Destroy();
        }

        public bool CanProbablySatisfy(Request req) {
            if (Held(req.Required.Type) is not { } lis) return false;
            var ct = 0;
            for (int ii = 0; ii < lis.Count; ++ii) {
                if (req.Matches(lis[ii]) && ++ct >= req.ReqCount)
                    return true;
            }
            return false;
        }
        public int NumHeld(Item item) => Held(item)?.Count ?? 0;
        public List<ItemInstance>? Held(Item item) => Inventory.TryGetValue(item, out var lis) ? lis : null;

        public int NumHeld(RecipeComponent comp) {
            var total = 0;
            for (int ii = 0; ii < Item.Items.Length; ++ii) {
                var item = Item.Items[ii];
                var match = comp.MatchesType(item);
                if (match is true) {
                    total += NumHeld(item);
                } else if (match is null) {
                    if (Held(item) is {} held)
                        for (int jj = 0; jj < held.Count; ++jj)
                            if (comp.Matches(held[jj]))
                                ++total;
                }
            }
            return total;
        }

        public bool Satisfied(RecipeComponent comp) => NumHeld(comp) >= comp.Count;

        public int NumCanCraft(Recipe recipe) {
            var ct = 99;
            foreach (var cmp in recipe.Components)
                ct = Math.Min(ct, NumHeld(cmp) / cmp.Count);
            return ct;
        }
    }

    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.ADVData.CurrentMap == "")
            throw new Exception("PJ24 was loaded with no current map.");
        Logs.Log("Starting PJ24 execution...");
        return new Executing(this, inst);
    }

    public override ADVData NewGameData() => new PJ24ADVData(new(SaveData.r.GlobalVNData)) {
        CurrentMap = Executing.House
    };
}
}