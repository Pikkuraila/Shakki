| Tiedosto | Kuvaus |
|-----------|--------|
| **GameDesignDoc.md** | Yleinen “living design document” — kuvaa pelin arkkitehtuurin, järjestelmät ja suunnitteluperiaatteet. Päivitä tätä jokaisen isomman muutoksen yhteydessä. |
| **FeatureRoadmap.md** | Konkreettinen tehtävä- ja milestone-lista. Päivitä kun toteutat, lisäät tai siirrät ominaisuuksia. |
| **Notes.md** | Nopeat ideat, ajatukset ja suunnitteluluonnokset. Saa olla sotkuinen – tarkoitettu vain luovaan ajatteluun. |

---

## 🔹 Työnkulku

1. **Päivitä dokumentteja** samalla kun teet koodimuutoksia.  
   → Näin design ja toteutus pysyvät aina linjassa.

2. **Commitoi dokumenttimuutokset yhdessä koodin kanssa.**  
   → Esim.  
   ```bash
   git add Docs/GameDesignDoc.md
   git commit -m "Add Fog of War modifier design and initial implementation"
