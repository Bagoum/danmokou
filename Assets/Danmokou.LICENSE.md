# Danmokou License

## About

Copyright (c) 2020-2022 Bagoum <reneedebatz@gmail.com>

Danmokou is a danmaku (bullet hell) engine built in C# for Unity. It is free (as in free speech) software. The source code is on Github at [Bagoum/danmokou](https://github.com/Bagoum/danmokou).



The source code in this project is present primarily under the directories `Assets/Danmokou/Plugins` (C#), `Assets/Danmokou/Rendering/Shaders` (HLSL), `Assets/Danmokou/CG` (HLSL), `Assets/Danmokou/UXML` (UI), as well as `Assets/Danmokou/Patterns` (Danmokou script files). All source code in this project is distributed under the MIT license, provided immediately below. 

```
Copyright (c) 2020-2022 Bagoum <reneedebatz@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

If you are making use of this project, please reproduce this license file or link to it ([Github link](https://github.com/Bagoum/danmokou/blob/master/Assets/Danmokou.LICENSE.md)).



This project has several submodules under the Assets folder (BRuH, LuA, SiMP, and SZYU). If you are viewing this license from source code, you can find licenses for each submodule within its folder. If you are viewing this license alongside a binary executable or ingame, you can find licenses for any included submodules distributed alongside this file. Note that submodules may have different licensing terms. 

- Danmokou only requires the SZYU submodule (licensed under MIT), which provides a visual novel engine through [Suzunoya](https://github.com/Bagoum/suzunoya) and [Suzunoya-Unity](https://github.com/Bagoum/suzunoya-unity). 



The project incorporates code or references packages from the following projects:

- [Newtonsoft.Json for Unity](https://github.com/jilleJr/Newtonsoft.Json-for-Unity) ([MIT](https://github.com/jilleJr/Newtonsoft.Json-for-Unity/blob/2a26db68c98003f28749203378a3863afa5e6885/LICENSE.md))
- [TonyViT/CurvedTextMeshPro](https://github.com/TonyViT/CurvedTextMeshPro) ([MIT](https://github.com/TonyViT/CurvedTextMeshPro/blob/96767f3394b71043da4ae8f42de114bb37f738b5/LICENSE))
- [liortal53/MissingReferencesUnity](https://github.com/liortal53/MissingReferencesUnity) ([Apache 2.0](https://github.com/liortal53/MissingReferencesUnity/blob/25449e5bcfe0ba917ff730f870e234cc147a19ba/LICENSE))
- [taisei-project/taisei](https://github.com/taisei-project/taisei) ([MIT-ish](https://github.com/taisei-project/taisei/blob/master/COPYING))
  - This project also incorporates some non-code resources from Taisei Project, listed below under "Non-Code Resources".

This project consumes the following projects as DLLs under Assets/Danmokou/Plugins/DLLs:

- [protobuf-net*](https://www.nuget.org/packages/protobuf-net) ([Apache 2.0](https://github.com/protobuf-net/protobuf-net/blob/22957abce76ba37f6401f5961aa5d8ace419afad/Licence.txt))
- [MathNet*](https://www.nuget.org/packages/MathNet.Numerics) ([MIT](https://github.com/mathnet/mathnet-numerics/blob/4b13ff4a5e7014997075df34a84d61f10bdd27b3/LICENSE.md))
- [System.Collections.Immutable](https://www.nuget.org/packages/System.Collections.Immutable/) ([MIT](https://github.com/dotnet/runtime/blob/71adfb003aa57f4c8801fc9079c9339342c58524/LICENSE.TXT))
- [LanguageServer.Contracts](https://github.com/Bagoum/LanguageServer.NET) ([MIT](https://github.com/Bagoum/LanguageServer.NET/blob/1b27be88bff80f85cc3511fbd71b825437b85c59/LICENSE.md))

The directory Assets/TextMeshPro contains default imports from the TextMeshPro plugin for Unity. TextMeshPro is licensed under the [Unity Companion License](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/license/LICENSE.html). 



The copyright of Touhou Project is owned by Team Shanghai Alice.

## Non-Code Resources

Note that many assets with unlisted licenses, such as those by DD and dairi, are not permitted for commercial use. 

The default menu background is by 紅月カイ on [Pixiv](https://www.pixiv.net/en/artworks/78519209).

Sound effects (Assets/Danmokou/Audio/SFX)
- manual: by Bagoum, licensed under CC BY 4.
- Many sound effects in "SFX/_SO/usedfiles" were created by Kenny Vleugels (kenney.nl) and are licensed thereby under CC0.
- Many sound effects in "SFX/_SO/usedfiles" by OSA (license text below).
- taisei: by Taisei Project (with modification) (license text below).
- sourced:
	- "fire shaker*" under CC0 from [here](https://freesound.org/people/shpira/sounds/317102/)
	- "crow caw" under CC0 from [here](https://freesound.org/people/_nuel/sounds/54973/)
	- "camera-snap1*" under CC-BY from [here](https://freesound.org/people/thecheeseman/sounds/51360/)
	- "camera-focus" under CC-BY from [here](https://freesound.org/people/Puniho/sounds/115126/)
	- "meter-activated" under CC-BY from [here](https://freesound.org/people/hykenfreak/sounds/248182/)
	- "achievement-1" under CC-BY from [here](https://freesound.org/people/LittleRobotSoundFactory/sounds/274179/)
	- "rain on windows" under CC-BY from [here](https://freesound.org/people/InspectorJ/sounds/346642/)

All assets under Assets/Danmokou/Kenney were created by Kenny Vleugels (kenney.nl) and are licensed thereby under CC0.

3D Resources (Assets/Danmokou/Models)

- Models/objects under MirrorShatter by André Cardoso, licensed under MIT.

Images (Assets/Danmokou/Sprites)
- BGPatterns/kuroma/*: by Kuroma on [Pixiv](https://www.pixiv.net/en/users/4702770).
- BGPatterns/kai_kasen_reimu: by 紅月カイ on [Pixiv](https://www.pixiv.net/en/artworks/78519209). Noncommercial use only.
- BGPatterns/buson-crows-crop: by Yosa Buson (now public domain).
- BGPatterns/nasa: by NASA, see below.
- BGPatterns/unsplash: miscellaneous Unsplash users, credited as follows:
	- [bamboo](https://unsplash.com/photos/AEmqD6qu434)
	- [shrine](https://unsplash.com/photos/mHtQ7rWEC4U)
	- [mountain](https://unsplash.com/photos/JgOeRuGD_Y4)
	- [space](https://unsplash.com/photos/4dpAqfTbvKA)
- entity/dot/kmap:  by danmaq (license in folder).
- entity/dot/dot_seija: by Len.
- entity/dot/dot_kasen, dot_junko: by Kirbio.
- entity/taisei_*: by Taisei Project (with modification).
- DD/*: by [DD](https://www.pixiv.net/en/users/4650367) . Noncommercial use only. 
- .py: Python scripts licensed by Bagoum under MIT.
- UI/faIcons/*: icons from [FontAwesome](https://github.com/FortAwesome/Font-Awesome), licensed under CC-BY 4. Modifications made.
- [UI/trophy.png](https://pixabay.com/vectors/success-badge-trophy-award-winner-5974538/)
- UI/pins/crow*: stock by Roy3D on [DeviantArt](https://www.deviantart.com/roy3d/art/Crows-2-PNG-Stock-412584565), modifications made.
- UI/commense: Yukari sprites by [dairi](https://www.pixiv.net/en/users/4920496). Noncommercial use only. 
- VN/Characters/dairi_*: by dairi.
- VN/Backgrounds:
  - Miscellaneous Unsplash users, credited as follows:
    - [shrine-room.jpg](https://unsplash.com/photos/M9NTnUlNiEM)
    - [shrine-courtyard.jpg](https://unsplash.com/photos/8ceNIk_TuiA)
    - [town.jpg](https://unsplash.com/photos/O-SKMhBFu3k)
    - [library.jpg](https://unsplash.com/photos/YLSwjSy7stw)
    - [field.jpg](https://unsplash.com/photos/YhZt03_OJKs)
    - [farm.jpg](https://unsplash.com/photos/QcBAZ7VREHQ)
    - [flower](https://unsplash.com/photos/h7_SyoBhHF0)
    - [forest-foggy](https://unsplash.com/photos/0K9H0yts_tE)
    - [shrine2](https://unsplash.com/photos/2ULo-3qFDgY)
    - [waterfall](https://pixabay.com/photos/waterfall-fall-epic-nature-light-7091641/)
    - [misty-lake](https://pixabay.com/photos/lake-fog-mist-sunrise-grass-haze-5722650/)
    - [bedroom-normal](https://pixabay.com/photos/bedroom-bed-apartment-room-416062/)
    - [pool](https://pixabay.com/photos/pool-rain-water-surface-background-4332152/)
  - Others:
    - []
- VN/Objects:
  - [yellowlily.png](https://commons.wikimedia.org/wiki/File:Lilium_canadense_(lit).jpg): Public domain
  - [yellowiris.png](https://commons.wikimedia.org/wiki/File:20140504Iris_pseudacorus2.jpg): CC0
  - [fleurdelys.png](https://commons.wikimedia.org/wiki/File:Fleur_de_lys_(or).svg): CC-BY
- items/gems*: under CC-BY from [Code Inferno Games (codeinferno.com)](https://opengameart.org/content/animated-spinning-gems)
- player/focus: by Taisei Project (with modification).
- bullets/straight-png/taisei_scythe: by Taisei Project (with modification).
- other files in bullets, UI, util, TransitionTextures, items, player: by Bagoum, licensed under CC-BY 4.

Assets/Danmokou/MiniProjects
- Cutins/Sprites: by dairi.
  - 
- VN/Sprites:

Some image resources were created with patterns from http://www.sda.nagoya-cu.ac.jp/sa08m13/image.html 
Some image resources were created with images from Unsplash. Here are all usages:
https://unsplash.com/photos/wXuzS9xR49M (used in Bad Apple UI frame)

Some images are from NASA. (NASA does not endorse this project.) Here are all usages:
https://images.nasa.gov/details-GSFC_20171208_Archive_e001982  (moon bullet sprite)
https://photojournal.jpl.nasa.gov/catalog/PIA22567
https://images.nasa.gov/details-PIA21073
https://images.nasa.gov/details-GSFC_20171208_Archive_e001925
https://images.nasa.gov/details-PIA19821
https://images.nasa.gov/details-PIA08653
https://images.nasa.gov/details-PIA17563
https://images.nasa.gov/details-PIA15415
https://images.nasa.gov/details-PIA00404
https://images.nasa.gov/details-PIA03096
https://unsplash.com/photos/rTZW4f02zY8
https://unsplash.com/photos/-hI5dX2ObAs
https://images.nasa.gov/details-PIA07841
https://images.nasa.gov/details-PIA03515
https://images.nasa.gov/details-PIA09962
https://images.nasa.gov/details-PIA17005
https://images.nasa.gov/details-PIA16438
https://images.nasa.gov/details-GSFC_20171208_Archive_e000245



## Font Licenses

For brevity, I am replicating the OFL license v1.1 exactly once.

### Generic OFL License (v1.1)

```
This Font Software is licensed under the SIL Open Font License, Version 1.1.
This license is copied below, and is also available with a FAQ at:
http://scripts.sil.org/OFL


-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded, 
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.
```

### Corporate Logo (CorpSt): OFL

See the OFL license above.

### EdoSz

Font from http://www.vicfieger.com/~font/handwr.html .

License terms (from http://www.vicfieger.com/~font/faq.html):

```
Do I require a license for using any of these fonts?

For free fonts: No license is required, no payment is required. These fonts may be used by anyone for any design or artistic purpose, whether it be personal, commercial, or charitable.
```

### Monoid: OFL

Copyright (c) 2015, Dave Gandy, Andreas Larsen and contributors.

See the OFL license above.

### Odibee Sans: OFL

Copyright 2017 The Odibee Sans Project Authors (https://github.com/barnard555/odibeesans).

See the OFL license above.

### Poiret: OFL

Copyright (c) 2011, Denis Masharov (denis.masharov@gmail.com).

See the OFL license above.

### Signika: OFL

Copyright (c) 2011 by Anna Giedryś (http://ancymonic.com), with Reserved Font Names "Signika".

See the OFL license above.

### Yasashisa Gothic

```
これらのフォントはフリー（自由な）ソフトウエアです。あらゆる改変の有無に関わらず、また商業的な利用であっても、自由にご利用、複製、再配布することができますが、全て無保証とさせていただきます。 

https://mplus-fonts.osdn.jp/about.html#license
```

Also now released under OFL (see the OFL license above).

### Fira Code: OFL

Copyright (c) 2014, The Fira Code Project Authors (https://github.com/tonsky/FiraCode).

See the OFL license above.

### YOz: OFL

Copyright (c) 2016-08-19, Y.Oz (Y.OzVox) (http://yozvox.web.fc2.com), with Reserved Font Name "Y.OzFont", "YOzFont", "Y.Oz" and "YOz".

See the OFL license above.

### Nova: OFL

Copyright (c) 2011, wmk69 (wmk69@o2.pl), with Reserved Font Name NovaSquare.

See the OFL license above.

### PromptFont: OFL

On Github here: https://github.com/Shinmera/promptfont/

See the OFL license above.

## Miscellaneous Licenses

### CC Licenses

[CC-BY 4.0](https://creativecommons.org/licenses/by/4.0/)

[CC-BY](https://creativecommons.org/licenses/by/3.0/)

[CC0](https://creativecommons.org/publicdomain/zero/1.0/)

### OSA Resources

English

```
-----------------------------------------------------------------------------------
Introduction
-----------------------------------------------------------------------------------

This is sound files use for creation.
Made by "Osabisi".

Web: THE MATCH-MAKERS
http://osabisi.sakura.ne.jp/m2/

*This web site are written in Japanese.


-----------------------------------------------------------------------------------
Terms of use
-----------------------------------------------------------------------------------

You can use my sound library (wav or mp3) for your creation with less restriction.
I want help your creation because I'm one of indie creator!

1) You can publish and sell your product that include my sounds.
  And You don't need to email me when develop or publish product.

2) I don't want money or any other repayment.

3) You can use my sounds for any type of content.
  Fun, Business, Education, Satire, Mature, Adult and so on.

4) Credit my name is optional.

5) You can edit my sounds.
  (e.g. rename, change pitch or gain, material for making new sound)


-----------------------------------------------------------------------------------
You must promise the following.
-----------------------------------------------------------------------------------

a) I'm not abandoned all of copyright.
  You must use my sound files for your part of creation.
  Don't distribute sound files only.

  You must email me and get permission if you want distribute sound files only.
  (It's include edited sound files.)

b) You must write source of sound files in documents
  when you enter a public contest to avoid law trouble.

c) I don't care any trouble with handling my sounds.


-----------------------------------------------------------------------------------
FAQ
-----------------------------------------------------------------------------------

 Q: Price free?
 A: Yes, I want no money. I'm making and distribute sound files since 2001
  and no money trouble. And I promise no trouble in future.

 Q: These sounds are made by yourself?
 A: Yes. All of sounds edited or recorded myself.

 Q: Who are you?
 A: I'm OSA, Japanese indie creator.
  main work is making sound effect and 2D dot sprite.

  Now I'm working with "ASTRO PORT".
  http://www.interq.or.jp/saturn/takuhama/dhc.html
  (Some games available on steam.)



Any other question? I'll answer you.

email : m3(a)osabisi.sakura.ne.jp
	 -> please replace (a) to @ , and don't use HTML email.

	I can read and write English. But please write mail subject in Japanese if you can.
	Because I may delete your email misunderstanding to spam.
	Example, "効果音に関する問い合わせ". Copy and paste to subject.

	Or copy and paste "Question about Your Sound files".

	I'll reply on 2-3 days. If you can't get reply, your mail may misunderstanding to spam.
	Please check your mail subject and text format and resend it.



[EOF] 
```

Japanese

```
=================================================================================
【分　　類】フリー効果音（wav形式）
【製作者名】OSA (Osabisi)
【配 布 元】ザ・マッチメイカァズ（http://osabisi.sakura.ne.jp/m2/）
【転載制限】素材そのもののみの転載は一切禁止します
【連 絡 先】m3＊osabisi.sakura.ne.jp
            (手動で｢*｣を｢@｣に変えてください)
=================================================================================

＜利用規約(2014年5月31日版)＞
　以下の約束を守って頂ければ 営利/非営利、表現内容の如何を問わず

　当方の素材をご利用いただくことが可能です。

　＊何らかの創作物の一部として使用せずに素材そのものを無断で転載/配布する
　　事は絶対にやめてください。
　（無断でWeb上にアップロードして共有したり、素材集CDに焼いて売るなどしてはいけません）

　＊当方のサイト上のファイルに直リンクを張って使う事も絶対にやめて下さい。
　　必ずDL（お持ち帰り）の上でご利用いただくようお願いいたします。

　＊素材の加工は自由に行っていただいて構いませんが、加工後の素材を
　　”素材として”広く配布する事は禁止いたします。どうしても配布を行いたい場合は
　　お問い合わせを頂ければ 検討の上 許可のお返事をできる場合があります。

　＊公のコンテストに応募する作品に使用する場合は、権利上のトラブルが
　　生じる恐れがありますので、添付書類など見える場所に 素材を引用した旨と
　　入手元である当方のサイトの情報を明記して下さい。

　＊作品および添付テキスト等の中に私の名前を出していただく義務はありません。
　　自分の名前を広める事に興味はありませんので、書いて頂かなくても一向に構いません。

　＊ダウンロード報告は不要です。
　　（作品が完成しましたらうちのBBSでぜひ宣伝してください！）

　＊音量の調整、ビットレートの変更、エンコード、デコードなどはご自分で行って下さい。

以上です。何か疑問や質問などがありましたら気軽にお問い合わせ下さい。


[EOF]
```





