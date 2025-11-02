
---

## 📁 **Docs/FeatureRoadmap.md**

```markdown
# 🚀 RogueChess Feature Roadmap

Tämä dokumentti toimii projektin "to-do listana" ja roadmapina.  
Tavoite: pitää kehitystyö jäsenneltynä ja helposti päivitettävänä Gitin kautta.

---

## 🔹 Kehityksen vaiheet

| Versio | Tavoite | Status |
|---------|----------|--------|
| **v0.101 – Core Loop** | Pelaajan ja AI:n perussiirrot, vuoronvaihto  | ✅ Valmis |
| **v0.103 - voitto kuninkaan syönnistä, lautojen generointi | ✅ Valmis  |
| **v0.104 - Kauppa skenen pohja ja kaupassa voi vaihdella nappuloiden paikkoja | ✅ Valmis  |
| **v0.105 – Kauppa & Meta** kaupasta ostettavat nappulat | 🔄 Työn alla |
| **v0.2 – Kauppa & Meta** | ShopScene, PlayerData, coin-järjestelmä. | 🔄 Työn alla |
| **v0.3 – Kampanja / Macropeli** | 3×20 makrolauta, boss-vuorot, kaupat ja eventit. | 🕓 Suunnitteilla |
| **v0.4 – Fog of War & Modifiers** | IBattleModifier-järjestelmä, visibility-palvelu. | ⏳ Suunnitteilla |
| **v0.5 – Progression & Tallennus** | PlayerData persistenssi + DifficultyCurve. | ⏳ Suunnitteilla |
| **v0.6 – Telemetria & Balanssi** | Lokitus + Balance Dashboard. | ⏳ Suunnitteilla |
| **v1.0 – Launch Candidate** | Täysi roguelite-kampanja, grafiikat, äänet, UI. | ⏳ Tulevaisuudessa |

---

## 🔹 Lyhyen aikavälin tehtävät

- [X] Tehdään pelilaudasta modulaarinen ja helposti generoitava.
- [X] Toteuta `GameState.CheckGameEnd()` ja `OnGameEnded`.
- [ ] Luo `ShopScene` ja UI.
- [X] Toteuta `PlayerData` (coins, ownedPieces, upgrades).
- [ ] Tee `CampaignState` + `EncounterSO`-malli.
- [ ] Tee `IBattleModifier` + pari esimerkkiä (Fog, Heal).
- [ ] Toteuta `RuleSetSO` + editor-validointi.
- [ ] Rakenna `BattleSim`-editorityökalu.
- [ ] Tee `Feature unlock` -järjestelmä makropelilaudalle.

---

## 🔹 Pitkän aikavälin suunnitelma

- [ ] Steam-versio: build pipeline ja julkaisuasetukset.  
- [ ] Erikoisnappulat (Cannon, Grasshopper, Amazon, Joker).  
- [ ] Boss-AI:t ja “Legendary”-tason laudat.  
- [ ] Äänimaisemat ja visuaaliset efektit.  
- [ ] UI-päivitys (retro/boardgame-estetiikka).  
- [ ] Lokalisointi (EN, FI).  
- [ ] Achievement-järjestelmä.  
- [ ] Workshop / mod support.

---

## 🔹 Design-periaatteet muistutuksena

- 💡 *Data over code* – kaikki sisältö ScriptableObjecteissa.  
- 🔁 *Iteratiivisuus* – kehitys etenee pienin, julkaistavin askelin.  
- 🧠 *Selkeys ennen kompleksisuutta* – AI ja laajennettavuus pidetään modulaarisina.  
- 🎯 *Yksi totuus* – tämä roadmap kertoo aina todellisen kehitystilan.
