# 🧭 Git Cheatsheet – RogueChess

Tämä tiedosto on pikaopas projektin versionhallinnan käyttöön.  
Sopii täydellisesti, jos tarvitset nopean muistin siitä mitä komentoa käyttää milloinkin.

---

## 🔹 Päivittäiset komennot

| Toiminto | Komento | Kuvaus |
|-----------|----------|--------|
| Näytä tilanne | `git status` | Näyttää mitkä tiedostot ovat muuttuneet. |
| Lisää kaikki muutokset | `git add .` | Valmistelee kaikki tiedostot commitointiin. |
| Tee commit | `git commit -m "kuvaus muutoksesta"` | Tallentaa muutokset paikallisesti. |
| Lähetä pilveen | `git push` | Lähettää commitit GitHubiin. |
| Hae uusimmat muutokset | `git pull` | Päivittää paikallisen version pilvestä. |

---

## 🔹 Yleiset tilanteet

**Uusi tiedosto lisätty:**  
```bash
git add Docs/NewFile.md
git commit -m "Add new documentation file"
git push


Korjasin virheen desingdocis.
git add Docs/GameDesignDoc.md
git commit -m "Fix typos in design doc"
git push


Tahdot tarkistaa, mitä muuttui ennen commitia:
git diff

🔹 Gitin rakenne pähkinänkuoressa

git add siirtää muutokset valmiiksi commitointiin

git commit luo version vain omalle koneelle

git push lähettää sen GitHubiin

git pull hakee uusimmat muutokset takaisin koneelle

🔹 Hyödyllisiä vinkkejä

Tee pieniä committeja.
Yksi commit = yksi järkevä muutos.

Käytä selkeitä commit-viestejä.
Hyvä: Add Fog of War rule system
Huono: update

Pushaa päivän päätteeksi.
Silloin mikään ei katoa, vaikka kone hajoaisi.

Älä pelkää committeja.
Ne eivät “julkaise” mitään — vasta push tekee