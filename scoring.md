# Systeme de notation

Les scores servent a prioriser les entreprises et les offres a traiter en premier. Ils sont recalcules depuis `/dashboard` ou `/jobs` apres import des entreprises, des offres et du CV.

## Profil candidat

Le profil candidat contient maintenant, en plus des elements detectes dans le CV, des preferences explicites utilisees par les nouvelles dimensions :

- localisations preferees ;
- preference de teletravail / hybride / presentiel ;
- salaire cible annuel.

Si une preference n'est pas renseignee, la rubrique correspondante vaut 0 et ajoute une raison de vigilance.

## Entreprises

Score total : 100 points.

| Rubrique | Maximum | Regle |
| --- | ---: | --- |
| Technique | 60 | Correspondance entre les competences detectees dans le CV et la stack connue de l'entreprise, completee par la stack des offres liees. |
| Domaine | 25 | Alignement entre les domaines detectes dans le CV et le domaine principal ou secondaire de l'entreprise. |
| Strategique | 15 | Signaux explicites de priorisation : domaine prioritaire/proche, URL carriere, notes qualifiees et offres rattachees. |

Detail domaine :

- domaine identique : 25 points ;
- domaine proche : 18 points ;
- domaine non aligne ou non renseigne : 0 point.

Detail strategique :

- domaine prioritaire ou proche du profil : 5 points ;
- URL carriere presente : 4 points ;
- notes entreprise presentes : 3 points ;
- au moins une offre rattachee : 3 points ;
- le total strategique est plafonne a 15 points.

Si une entreprise a des offres liees, son score global est au minimum le meilleur score global de ses offres. Les rubriques technique, domaine et strategique restent le detail propre a l'entreprise.

## Offres

Score total : 100 points.

| Rubrique | Maximum | Regle |
| --- | ---: | --- |
| Technique | 35 | Correspondance entre la stack technique de l'offre et les competences detectees dans le CV. |
| Experience | 25 | Compatibilite entre le niveau d'experience du CV et le niveau attendu par l'offre. |
| Role | 15 | Alignement entre le role detecte dans le CV et le poste propose. |
| Domaine | 10 | Alignement avec un secteur deja detecte dans le CV. |
| Localisation | 5 | Compatibilite entre `job.location`, `remote_policy`, les localisations preferees et la preference teletravail. |
| Salaire | 5 | Compatibilite entre `salary_min` / `salary_max` et le salaire cible du profil. |
| Strategique | 5 | Signaux d'actionnabilite de l'offre : URL, description detaillee et publication recente. |

Detail experience :

- profil compatible avec le niveau attendu : 25 points ;
- profil un niveau sous le niveau attendu : 20 points ;
- profil deux niveaux sous le niveau attendu : 10 points ;
- profil trop eloigne : 0 point ;
- si l'offre ne donne pas de niveau, l'application infere le niveau depuis l'intitule ou la description ; sans indice, elle suppose un niveau confirme, environ 3-4 ans d'experience.

Detail role :

- role aligne : 15 points ;
- role adjacent backend / fullstack ou frontend / fullstack : 7 points ;
- role non aligne : 0 point.

Detail localisation :

- preference remote/teletravail et offre remote, teletravail ou hybride : 5 points ;
- sinon, localisation de l'offre contenant une localisation preferee : 5 points ;
- preference absente ou localisation non alignee : 0 point.

Detail salaire :

- salaire maximum de l'offre, ou salaire minimum si le maximum est absent, superieur ou egal au salaire cible : 5 points ;
- salaire au moins egal a 90 % de la cible : 3 points ;
- salaire absent, cible absente ou salaire inferieur : 0 point.

Detail strategique offre :

- URL d'offre presente : 2 points ;
- description detaillee : 2 points ;
- publication datant de moins de 45 jours : 1 point.
