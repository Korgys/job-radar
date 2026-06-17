# Job Radar Local

Application web locale V0.2 pour importer des entreprises, des offres et un CV, calculer des scores de compatibilite, visualiser les entreprises sur une carte Leaflet avec OpenStreetMap et generer un rapport Markdown.

## Prerequis

- .NET SDK 8 ou plus recent
- Node.js 20 ou plus recent
- npm

## Lancement

Backend :

```bash
cd backend
dotnet run
```

Frontend :

```bash
cd frontend
npm install
npm run dev
```

Ouvrir ensuite :

```txt
http://127.0.0.1:5173
```

L'API ecoute par defaut sur :

```txt
http://127.0.0.1:5087
```

## Carte

La page `/map` utilise OpenStreetMap via Leaflet. Un acces reseau est necessaire pour charger les tuiles.

Les donnees entreprises, offres, scores et rapports restent stockes localement.

## Notation

Le systeme de notation est decrit dans [scoring.md](scoring.md).

## Parcours de test

1. Aller dans `/companies` et importer `data/samples/strasbourg-area-companies.csv`.
2. Aller dans `/jobs` et importer `data/samples/strasbourg-area-jobs.csv`.
3. Aller dans `/profile` et importer `data/samples/cv.txt`.
4. Recalculer les scores depuis `/dashboard` ou `/jobs`.
5. Aller dans `/map` pour voir les entreprises et filtrer les resultats.
6. Aller dans `/report` et generer un rapport Markdown.

Les rapports sont ecrits dans :

```txt
data/reports/
```

La base SQLite locale est creee automatiquement dans :

```txt
data/job-radar-local.db
```

## Prompt de recherche entreprises

Les prompts du dossier `prompts/` servent a generer ou completer les CSV importables.

### Entreprises

Le prompt `prompts/update-companies.md` sert a generer ou completer le CSV entreprises.

1. Remplacer `{{VILLE}}` par la ville cible.
2. Remplacer `{{RAYON_KM}}` par le rayon de recherche.
3. Remplacer `{{CSV_EXISTANT}}` par le contenu actuel du CSV, ou laisser vide.
4. Importer le CSV obtenu depuis la page `/companies`.

### Offres

Le prompt `prompts/update-jobs.md` sert a synchroniser le CSV offres.

1. Remplacer `{{VILLE}}` par la ville cible.
2. Remplacer `{{RAYON_KM}}` par le rayon de recherche.
3. Remplacer `{{CV_CANDIDAT}}` par le CV, ou laisser vide.
4. Remplacer `{{CSV_EXISTANT}}` par le contenu actuel du CSV, ou laisser vide.
5. Importer le CSV obtenu depuis la page `/jobs`.

## Formats CSV

### Entreprises

Colonnes attendues :

```csv
name,domain,secondary_domains,city,address,latitude,longitude,website,career_url,linkedin_url,glassdoor_url,known_stack,notes,logo_url
```

Champs obligatoires : `name`, `domain`, `city`, `latitude`, `longitude`.

Listes separees par `;` : `secondary_domains`, `known_stack`.

Une entreprise deja presente avec le meme nom et la meme ville est mise a jour.

### Offres

Colonnes attendues :

```csv
company_name,title,location,remote_policy,contract,salary_min,salary_max,seniority,job_type,stack,description,url,publication_date
```

Champs obligatoires : `company_name`, `title`.

Liste separee par `;` : `stack`.

Les doublons sont evites avec la combinaison `company_name + title + url`.

## CV

Formats supportes en V0.2 :

- `.txt`
- `.md`

PDF et DOCX ne sont pas parses dans cette version. Le parsing est isole derriere `ICvParsingService` pour permettre un ajout ulterieur sans modifier les endpoints.

Le CV fourni est fictif et ne doit pas contenir de donnees personnelles. Vous pouvez bien sûr utiliser l'application avec votre propre CV mais ne l'archivez pas dans le répertoire Git.

## Tests

Depuis la racine du projet :

```bash
dotnet test
```

## Structure

```txt
backend/
frontend/
data/
  samples/
    strasbourg-area-companies.csv
    strasbourg-area-jobs.csv
    cv.txt
  reports/
prompts/
  update-companies.md
  update-jobs.md
README.md
scoring.md
```
