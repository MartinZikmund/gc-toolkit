# Nabídka v Microsoft Storu — cs-CZ

Návrh textů pro stránku **Store listing** v Partner Center, trh **Česko / čeština**.
Jedna sekce na jedno pole Partner Center, s limitem v nadpisu a s aktuální délkou nad každým
blokem. Vše uvnitř bloku se vkládá doslova, vše mimo něj je poznámka pro toho, kdo formulář
vyplňuje.

Terminologie záměrně kopíruje `src/GcToolkit/Strings/cs/Resources.resw`, aby se nabídka
ve Storu shodovala s tím, co uživatel v aplikaci opravdu uvidí. Když se změní překlad v aplikaci,
změňte i tento soubor.

Počty znaků zahrnují konce řádků — tak je počítá i Partner Center.

---

## Název aplikace

Partner Center rezervuje **jeden název pro celý produkt**; nabídka pro jednotlivé trhy vlastní
pole pro název nemá. Ať se rozhodnete jakkoli, čeští uživatelé uvidí přesně stejný řetězec jako
američtí — a musí dobře fungovat i v české větě.

Rozhodnutí o značce je otevřené a patří vlastníkovi projektu; kandidáti, argumenty a omezení
ohledně ochranných známek jsou v [`../en-US/listing.md`](../en-US/listing.md).

Poznámka k češtině: ze tří kandidátů se v české větě nejlépe skloňuje **GC Toolkit**
(„v GC Toolkitu“), *Cachewright* zůstane nesklonný a *Puzzle Cacher* působí česky nejhůř.

---

## Krátký popis — limit 270 znaků

Délka: **262**

```text
43 nástrojů pro luštění mystery keší, plně offline: převody a projekce souřadnic, klasické šifry, číselné posloupnosti a kódové tabulky jako morseovka, Braillovo písmo či semaforová abeceda. Zdarma, otevřený zdrojový kód, česky i anglicky. Bez účtu a bez reklam.
```

---

## Popis — limit 10 000 znaků

Délka: **4053**

```text
GC Toolkit je bezplatná nativní sada nástrojů pro luštění mystery keší. Všechno běží přímo ve vašem zařízení. Žádný účet, žádné přihlašování, žádné reklamy a žádný přístup k síti — aplikace nikdy neotevře spojení, takže nic, co do ní vložíte, ji neopustí. Spočítejte projekci bez signálu, rozluštěte šifru ve vlaku a zavřete aplikaci s jistotou, že se nikam nic neodeslalo.

Třiačtyřicet nástrojů v pěti kategoriích.

SOUŘADNICE
- Převod souřadnic mezi formáty WGS84 a UTM
- Vzdálenost a azimut mezi dvěma body
- Projekce bodu z výchozího bodu, azimutu a vzdálenosti

ŠIFRY
Caesarova šifra včetně ROT13, s přehledem všech posunů najednou, takže výsledek prostě přečtete ze seznamu. Vigenèrova šifra a Gronsfeldova šifra, její varianta s číselným klíčem. Afinní šifra s automatickým dopočítáním neznámého klíče. Šifra Atbaš. Baconova dvojková šifra po pěticích symbolů. Substituční šifra s vlastní 26písmennou abecedou nebo s abecedou vytvořenou z klíčového slova. Kámasútra šifra s dvojicemi písmen. Vernamova jednorázová tabulka. Plotová šifra a skytalé — obě umí neznámý klíč zkusit hrubou silou a nabídnout kandidáty. Trithemiova šifra, bezklíčový předchůdce Vigenèrovy šifry. Multitap na staré telefonní klávesnici. A Kennyho kód „mmm / mpp / mpf“ ze South Parku.

ČÍSLA
Převodník číselných soustav (dvojková, osmičková, desítková, šestnáctková). Římské číslice v obou směrech. Převod úhlů mezi stupni, radiány, gradiány, dílky, otáčkami a kompasovými body. Test prvočíselnosti, n-té prvočíslo, pořadí prvočísla, nejbližší prvočíslo a rozklad na prvočísla. Fibonacciho a Lucasova posloupnost, vyhledatelné podle pozice i podle hodnoty. Collatzova posloupnost 3n+1 včetně nejtvrdošíjnějšího výchozího čísla do zvoleného limitu. Kaprekarův postup ke konstantě 6174. A vyhledávání v desetinném rozvoji čísel π, e a φ — prvních N číslic, číslice na pozici, rozsah pozic nebo všechny výskyty hledané posloupnosti.

ABECEDY A KÓDOVÉ TABULKY
Morseova abeceda. Braillovo písmo 1. stupně. Hláskovací abeceda NATO a deset historických variant, oběma směry. Semaforová abeceda. Mezinárodní námořní signální vlajky. Klepací kód podle Polybiova čtverce i s vykresleným vzorem klepání. Barevný kód rezistoru — z proužků na hodnotu i naopak. Převod barev mezi hex, RGB, CMY, CMYK, HSL, HSV, HSI, YIQ, YUV a YCbCr. Přepis textu mezi rozložením QWERTY a Dvorak podle polohy kláves. Brainfuck a varianty Ook!, které aplikace rovnou spustí. A převod GC kódu na interní ID a zpět.

TEXT
Kódy znaků dekadicky, šestnáctkově, osmičkově i dvojkově v celém rozsahu Unicode. Čísla na písmena podle A1Z26 a zpět. Hodnota slova se součtem zredukovaným na ciferný kořen. A obracení, míchání či otáčení textu po řetězci, řádku, slově nebo znaku.

JAK NAJÍT SPRÁVNÝ NÁSTROJ
Katalog se dá prohledávat a je členěný do kategorií, každý nástroj si můžete přidat mezi oblíbené. Oblíbené a naposledy použité nástroje jsou rovnou na úvodní obrazovce, takže těch pět, které opravdu používáte, máte na jedno klepnutí a nemusíte je hledat mezi třiačtyřiceti.

PLATFORMY A JAZYKY
Postaveno na Uno Platform z jediné kódové základny. Tato nabídka pokrývá Windows 10 verze 1809 a novější. Aplikace je kompletně přeložená do češtiny a angličtiny — všechny popisky, nápovědy i výsledky, ne jen menu — a jazyk přepnete v Nastavení bez zásahu do jazyka systému. Světlý, tmavý i systémový motiv, ovládání z klávesnice a rozvržení, které se přizpůsobí od úzkého okna po celou šířku obrazovky.

OTEVŘENÝ ZDROJOVÝ KÓD
GC Toolkit je svobodný software pod licencí GNU General Public License verze 3. Kompletní zdrojový kód, seznam chyb i historii vydání najdete na:

https://github.com/MartinZikmund/gc-toolbox

Hlášení chyb, překlady i nápady na nové nástroje jsou tam vítané. Pokud vám nějaký nástroj chybí, založte tam issue — katalog je navržený tak, aby přidání dalšího nástroje bylo snadné.

Geocaching a Geocaching.com jsou ochranné známky společnosti Groundspeak, Inc. Tato aplikace je nezávislý projekt a není se společností Groundspeak, Inc. nijak spojena, není jí podporována ani sponzorována.
```

> Poznámky k překladu:
> - Názvy nástrojů, kategorií i formulace jsou převzaté z `src/GcToolkit/Strings/cs/Resources.resw`
>   (např. *Projekce bodu*, *Plotová šifra*, *Skytalé*, *Klepací kód*, *Hláskovací abeceda*,
>   *Barevný kód rezistoru*). Nepřekládejte je znovu — nabídka by se rozešla s aplikací.
> - „Mystery keš“ je to, co čeští kačeři skutečně říkají; „puzzle cache“ se v české komunitě
>   nepoužívá, proto v české verzi není.
> - Slovo *issue* je v české vývojářské mluvě zavedené a čtenář, který chce chybu nahlásit, ho
>   na GitHubu stejně uvidí; překlad („problém“, „hlášení“) by mátl víc než pomohl.
> - Počet nástrojů (**43**) i výčty po kategoriích ověřte před každým odesláním — katalog vzniká
>   při kompilaci z atributů `[Tool(...)]` v `src/GcToolkit.Core/ViewModels/Tools/`.

---

## Novinky v této verzi — limit 1 500 znaků

Délka: **409**

```text
První veřejné vydání.

43 nástrojů pro mystery keše v kategoriích souřadnice, šifry, čísla, abecedy a text. Prohledávatelný katalog s oblíbenými a naposledy použitými nástroji. Kompletní český i anglický překlad, jazyk se přepíná v Nastavení. Světlý, tmavý a systémový motiv. Funguje zcela offline a bez účtu.

Našli jste chybu nebo vám nějaký nástroj chybí? https://github.com/MartinZikmund/gc-toolbox/issues
```

---

## Hledané výrazy — max. 7, každý do 30 znaků

Uživatelům se nezobrazují, slouží jen vyhledávání ve Storu. Délky v hranatých závorkách.

```text
luštění mystery keší     [20]
mystery keš              [11]
převod souřadnic         [16]
projekce bodu            [13]
luštění šifer            [13]
morseova abeceda         [16]
caesarova šifra          [15]
```

> Stejně jako v anglické verzi tu **záměrně chybí „geocaching“** — je to ochranná známka
> společnosti Groundspeak a jako skrytý klíčový výraz je to šedá zóna. Rozhodnutí je na
> vlastníkovi projektu; zdůvodnění je v [`../en-US/listing.md`](../en-US/listing.md).
>
> Diakritiku ve výrazech ponechte. Vyhledávání ve Storu ji sice většinou normalizuje, ale výrazy
> bez diakritiky vypadají v Partner Center jako překlep a nic navíc nepřinesou.

---

## Kategorie a podkategorie

Kategorie se v Partner Center nastavuje **jednou pro celý produkt**, ne pro každý jazyk zvlášť.
Doporučení a zdůvodnění (**Utilities & tools**) je v [`../en-US/listing.md`](../en-US/listing.md).

---

## Dodatečné licenční podmínky

Pole „Additional license terms“ je v Partner Center vedené u produktu (stránka Properties), ne
u jednotlivých jazykových nabídek — **ověřte to při vyplňování**, formulář se čas od času mění.
Do formuláře jde anglická verze z [`../en-US/listing.md`](../en-US/listing.md).

Český překlad níže je pro použití v aplikaci (stránka *O aplikaci*) nebo v README, kdyby se
hodil:

```text
GC Toolkit je svobodný software. Tato aplikace je šířena podle podmínek licence GNU General Public License verze 3 nebo (dle vaší volby) kterékoli pozdější verze vydané nadací Free Software Foundation.

Vaše užívání, úpravy a další šíření této aplikace se řídí licencí GNU General Public License. V rozsahu, v jakém by standardní licenční podmínky pro aplikace Microsoftu byly s licencí GNU General Public License v rozporu, má přednost GNU General Public License.

Tento program je šířen v naději, že bude užitečný, avšak BEZ JAKÉKOLI ZÁRUKY; neposkytují se ani odvozené záruky PRODEJNOSTI nebo VHODNOSTI PRO URČITÝ ÚČEL. Podrobnosti najdete v licenci GNU General Public License.

Kompletní odpovídající zdrojový kód je dostupný na https://github.com/MartinZikmund/gc-toolbox; kopie licence je součástí zdrojového kódu.
```

---

## Informace o autorských právech a ochranných známkách

Pole je součástí jazykové nabídky, takže se vyplňuje zvlášť. Limit 200 znaků.

Délka: **147**

```text
© 2026 Martin Zikmund. Licencováno podle GNU General Public License v3.0. Geocaching® je registrovaná ochranná známka společnosti Groundspeak, Inc.
```

> Delší formulace („není se společností Groundspeak, Inc. nijak spojena…“) se do 200 znaků vedle
> copyrightu nevejde, proto je na konci popisu. Nechte ji tam.

---

## Adresy a kontakt

| Pole | Hodnota | Stav |
| --- | --- | --- |
| Zásady ochrany osobních údajů | **TODO** | **Povinné.** Aplikace nic nesbírá a nekomunikuje po síti, Partner Center přesto vyžaduje funkční odkaz. Odkaz může vést na anglickou stránku, ale česká verze působí líp — pokud vznikne `PRIVACY.md`, přidejte i `PRIVACY.cs.md`. |
| Web | `https://github.com/MartinZikmund/gc-toolbox` | Nepovinné; do doby, než vznikne stránka produktu, stačí repozitář. |
| Kontakt na podporu | **TODO** | `https://github.com/MartinZikmund/gc-toolbox/issues`, nebo `martin@zikmund.dev`. Viz anglická verze. |

---

## Pole, která zůstávají prázdná

- **Krátký název, Řadicí název, Hlasový název** — pozůstatky dlaždic a Cortany, nic z toho tu
  není potřeba.
- **Upoutávky a snímky 360°** — pro první vydání žádné.
