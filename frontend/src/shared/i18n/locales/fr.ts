// fr — French.
//
// Edit: dealership vocabulary, not literal translation. A `lead` is a *demande*
//       and a `deal` is a *vente*; "piste" and "affaire" are marketing and
//       banking words that a French vehicle salesperson does not use for these.
//       Accounting terms follow the plan comptable: *balance générale*, *grand
//       livre*, *exercice*.
//
//       Typography is French typography: a narrow no-break space ( )
//       before ? ! : and ; — a plain space lets the punctuation wrap onto the
//       next line on its own, which looks like a rendering fault. Quotation
//       marks are « ». The apostrophe is ’ and not '.

import type { Catalogue } from '../index';

export const fr: Catalogue = {
  'app.name': 'DealerFOSS',
  'common.loading': 'Chargement…',
  'common.save': 'Enregistrer',
  'common.saving': 'Enregistrement…',
  'common.cancel': 'Annuler',
  'common.close': 'Fermer',
  'common.retry': 'Réessayer',
  'common.search': 'Rechercher',
  'common.searching': 'Recherche…',
  'common.none': 'Aucun',
  'common.all': 'Tous',
  'common.yes': 'Oui',
  'common.no': 'Non',
  'common.back': 'Retour',
  'common.continue': 'Continuer',
  'common.unexpected': 'Une erreur s’est produite. Réessayez.',
  'common.notPermitted': 'Vous n’avez pas l’autorisation de voir ceci.',
  'common.unreachable': 'Impossible de joindre le serveur. Est-il démarré ?',

  'error.network': 'Impossible de joindre le serveur. Est-il démarré ?',
  'error.invalidCredentials':
    'Cette adresse e-mail et ce mot de passe ne correspondent à aucun compte.',
  'error.sessionRequired': 'Connectez-vous pour utiliser cette fonction.',
  'error.adminSessionRequired':
    'Connectez-vous en tant qu’administrateur pour accéder à la console. Une connexion de concession n’y donne pas accès.',
  'error.sessionInvalid': 'Votre session a pris fin. Reconnectez-vous.',
  'error.antiForgeryFailed':
    'Ce navigateur ne détient plus de jeton correspondant à sa session. Reconnectez-vous.',
  'error.secondFactorRejected': 'Ce code n’a pas été accepté.',
  'error.secondFactorRequired':
    'Votre rôle exige la connexion en deux étapes. Configurez-la pour accéder au reste de l’application.',
  'error.mfaNotEnrolled': 'La connexion en deux étapes n’est pas configurée sur ce compte.',
  'error.mfaAlreadyOn': 'La connexion en deux étapes est déjà activée sur ce compte.',
  'error.notATenantCaller':
    'Un administrateur ne peut pas agir en tant qu’utilisateur d’une concession.',
  'error.tenantRequired': 'Indiquez de quel groupe de concessions il s’agit.',
  'error.tenantNotFound': 'Aucun groupe de concessions actif ne porte ce nom.',

  'shell.skipToContent': 'Aller au contenu',
  'shell.mainNavigation': 'Principal',
  'shell.signOut': 'Se déconnecter',
  'shell.shortcuts': 'Raccourcis',
  'shell.shortcutsTitle': 'Raccourcis clavier ( ? )',
  'shell.language': 'Langue',
  'shell.appearance': 'Apparence',

  'nav.dashboard': 'Ce mois-ci',
  'nav.customers': 'Clients',
  'nav.leads': 'Demandes',
  'nav.deals': 'Ventes',
  'nav.stock': 'Stock',
  'nav.workshop': 'Atelier',
  'nav.parts': 'Pièces',
  'nav.trialBalance': 'Balance générale',
  'nav.books': 'Les comptes',
  'nav.records': 'Données',
  'nav.staff': 'Personnel',
  'nav.secondFactor': 'Connexion en deux étapes',

  'appearance.auto': 'Auto',
  'appearance.autoHint': 'Suivre cet appareil',
  'appearance.light': 'Clair',
  'appearance.lightHint': 'Toujours clair',
  'appearance.dark': 'Sombre',
  'appearance.darkHint': 'Toujours sombre',

  'signIn.lede': 'Connectez-vous à votre concession.',
  'signIn.dealerGroup': 'Groupe de concessions',
  'signIn.email': 'E-mail',
  'signIn.password': 'Mot de passe',
  'signIn.submit': 'Se connecter',
  'signIn.submitting': 'Connexion…',
  'signIn.codeLede':
    'Saisissez le code à six chiffres de votre application d’authentification, ou l’un de vos codes de secours.',
  'signIn.code': 'Code',
  'signIn.checking': 'Vérification…',
  'signIn.startAgain': 'Recommencer',

  'setPassword.title': 'Définissez votre mot de passe',
  'setPassword.lede':
    'Votre responsable vous a remis un code. Utilisez-le une seule fois ici pour choisir un mot de passe connu de vous seul — personne dans la concession ne peut voir ce que vous choisissez.',
  'setPassword.dealership': 'Concession',
  'setPassword.email': 'E-mail',
  'setPassword.code': 'Code',
  'setPassword.password': 'Nouveau mot de passe',
  'setPassword.passwordHint':
    'Au moins 12 caractères. C’est la longueur qui rend un mot de passe difficile à deviner.',
  'setPassword.again': 'Confirmez le nouveau mot de passe',
  'setPassword.mismatch': 'Ces deux mots de passe ne correspondent pas.',
  'setPassword.failed': 'Cela n’a pas fonctionné.',
  'setPassword.submit': 'Définir mon mot de passe',
  'setPassword.doneTitle': 'Votre compte est prêt',
  'setPassword.doneLede':
    'Connectez-vous avec votre adresse e-mail et le mot de passe que vous venez de choisir.',
  'setPassword.toSignIn': 'Aller à la connexion',

  'secondFactor.title': 'Connexion en deux étapes',
  'secondFactor.required':
    'Votre concession exige la connexion en deux étapes pour votre rôle. Tant que vous ne l’avez pas configurée, cet écran est le seul que vous pouvez utiliser.',
  'secondFactor.intro':
    'Ensuite, la connexion demandera un code à six chiffres généré par une application sur votre téléphone, en plus de votre mot de passe. Google Authenticator, Authy et 1Password conviennent tous.',
  'secondFactor.start': 'Commencer',
  'secondFactor.starting': 'Démarrage…',
  'secondFactor.pointApp': 'Pointez votre application d’authentification vers ce carré.',
  'secondFactor.qrTitle': 'Scannez ceci avec votre application d’authentification',
  'secondFactor.cannotScan': 'Impossible de le scanner ?',
  'secondFactor.typeInstead': 'Saisissez plutôt ceci à la main dans l’application :',
  'secondFactor.enterCode': 'Saisissez maintenant le code affiché',
  'secondFactor.turnOn': 'Activer',
  'secondFactor.checking': 'Vérification…',
  'secondFactor.notYet':
    'Rien n’a encore changé pour la connexion. Cela ne prendra effet qu’une fois le code ci-dessus accepté.',
  'secondFactor.onNow':
    'La connexion en deux étapes est activée. Désormais, un code vous sera demandé après votre mot de passe.',
  'secondFactor.saveTitle': 'Conservez-les en lieu sûr',
  'secondFactor.saveLede':
    'Chacun ne fonctionne qu’une seule fois, et uniquement si vous perdez votre téléphone. C’est la seule fois où ils seront affichés.',
  'secondFactor.recoveryCodes': 'Codes de secours',

  'shortcuts.title': 'Raccourcis clavier',
  'shortcuts.space': 'Espace',
  'shortcuts.note':
    'Les raccourcis sont ignorés pendant que vous saisissez du texte, afin de ne jamais avaler un caractère que vous vouliez écrire.',
  'shortcuts.goDashboard': 'Aller au tableau de bord',
  'shortcuts.goStock': 'Aller au stock',
  'shortcuts.goCustomers': 'Aller aux clients',
  'shortcuts.goLeads': 'Aller aux demandes',
  'shortcuts.goDeals': 'Aller aux ventes',
  'shortcuts.goWorkshop': 'Aller à l’atelier',
  'shortcuts.goParts': 'Aller aux pièces',
  'shortcuts.goBooks': 'Aller aux comptes',
  'shortcuts.monthBefore': 'Sur le tableau de bord : le mois précédent',
  'shortcuts.monthAfter': 'Sur le tableau de bord : le mois suivant',
  'shortcuts.thisMonth': 'Sur le tableau de bord : revenir à ce mois-ci',
  'shortcuts.showList': 'Afficher cette liste',

  // Vocabulaire du métier : « en préparation » plutôt que « reconditionnement »,
  // « visite spontanée » plutôt que « walk-in », et une voiture retenue par une
  // vente est « réservée », pas « en attente ».
  'enum.inventoryStatus.Incoming': 'En arrivage',
  'enum.inventoryStatus.Reconditioning': 'En préparation',
  'enum.inventoryStatus.Available': 'Disponible',
  'enum.inventoryStatus.OnHold': 'Réservé',
  'enum.inventoryStatus.Sold': 'Vendu',
  'enum.inventoryStatus.Removed': 'Retiré',

  // Une demande est féminine, d’où les accords.
  'enum.leadStatus.New': 'Nouvelle',
  'enum.leadStatus.Working': 'En cours',
  'enum.leadStatus.Appointment': 'Rendez-vous',
  'enum.leadStatus.Won': 'Gagnée',
  'enum.leadStatus.Lost': 'Perdue',

  'enum.leadSource.WalkIn': 'Visite spontanée',
  'enum.leadSource.Phone': 'Téléphone',
  'enum.leadSource.Website': 'Site web',
  'enum.leadSource.Referral': 'Recommandation',
  'enum.leadSource.Marketplace': 'Place de marché',
  'enum.leadSource.Unknown': 'Inconnue',

  'enum.dealStatus.Draft': 'Brouillon',
  'enum.dealStatus.Submitted': 'Soumise',
  'enum.dealStatus.Approved': 'Approuvée',
  'enum.dealStatus.Delivered': 'Livrée',
  'enum.dealStatus.Lost': 'Perdue',

  'enum.chargeKind.VehiclePrice': 'Prix du véhicule',
  'enum.chargeKind.Fee': 'Frais',
  'enum.chargeKind.Discount': 'Remise',
  'enum.chargeKind.Accessory': 'Accessoire',

  'enum.repairOrderStatus.Booked': 'Planifié',
  'enum.repairOrderStatus.InProgress': 'En cours',
  'enum.repairOrderStatus.Completed': 'Terminé',
  'enum.repairOrderStatus.Invoiced': 'Facturé',
  'enum.repairOrderStatus.Cancelled': 'Annulé',

  'enum.serviceLineKind.Labour': 'Main-d’œuvre',
  'enum.serviceLineKind.Part': 'Pièce',
  'enum.serviceLineKind.Sublet': 'Sous-traité',

  'enum.financeProductKind.Warranty': 'Garantie',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Contrat d’entretien',
  'enum.financeProductKind.Protection': 'Protection',
  'enum.financeProductKind.Other': 'Autre',

  'enum.periodState.Open': 'Ouvert',
  'enum.periodState.Closed': 'Clôturé',

  'enum.booksState.NotOpened': 'Non ouvert',
  'enum.booksState.Open': 'Ouvert',
  'enum.booksState.Closed': 'Clôturé',
  'enum.booksState.Unknown': 'Inconnu',

  'enum.importKind.Customers': 'Clients',
  'enum.importKind.Vehicles': 'Véhicules',

  'enum.tenantStatus.Active': 'Actif',
  'enum.tenantStatus.Suspended': 'Suspendu',
  'enum.tenantStatus.Provisioning': 'En cours de création',
  'enum.tenantStatus.Archived': 'Archivé',

  'stock.title': 'Stock',
  'stock.status': 'Statut',
  'stock.loading': 'Chargement du stock…',
  'stock.denied':
    'Vous n’avez pas accès au stock de ce site. Demandez à un responsable si cela vous semble anormal.',
  'stock.failed': 'Impossible de charger le stock.',
  'stock.empty':
    'Rien pour l’instant. Les véhicules apparaissent une fois entrés en stock.',
  'stock.onlyStockNumber': 'Affichage du numéro de stock {stock} uniquement.',
  'stock.showEverything': 'Tout afficher',
  'stock.colStock': 'N° de stock',
  'stock.colVehicle': 'Véhicule',
  'stock.colVin': 'VIN',
  'stock.colStatus': 'Statut',
  'stock.count': { one: '{count} véhicule en stock', other: '{count} véhicules en stock' },
  'stock.countCapped': 'Les {count} premiers véhicules en stock. Il peut y en avoir d’autres.',
  'stock.cappedNote':
    'Affichage des {count} premiers. Il peut y en avoir d’autres — affinez avec le filtre de statut en attendant la pagination.',

  // Vocabulaire du plan comptable général.
  'enum.accountKind.Asset': 'Actif',
  'enum.accountKind.Liability': 'Passif',
  'enum.accountKind.Equity': 'Capitaux propres',
  'enum.accountKind.Revenue': 'Produits',
  'enum.accountKind.Expense': 'Charges',

  'trialBalance.title': 'Balance générale',
  'trialBalance.loading': 'Calcul en cours…',
  'trialBalance.denied': 'Vous n’avez pas accès à ces chiffres.',
  'trialBalance.failed': 'Impossible de charger les soldes.',
  'trialBalance.empty':
    'Aucune écriture pour l’instant. Les écritures apparaissent ici une fois un véhicule livré.',
  'trialBalance.inBalance': 'Équilibrée — débits et crédits s’élèvent tous deux à {total}.',
  'trialBalance.outOfBalance':
    'Déséquilibre de {difference}. Quelque chose s’est perdu en cours de route.',
  'trialBalance.colCode': 'Numéro',
  'trialBalance.colAccount': 'Compte',
  'trialBalance.colKind': 'Nature',
  'trialBalance.colDebits': 'Débits',
  'trialBalance.colCredits': 'Crédits',
  'trialBalance.colBalance': 'Solde',
  'trialBalance.total': 'Total',

  'enum.customerKind.Person': 'Particulier',
  'enum.customerKind.Business': 'Entreprise',

  'customers.title': 'Clients',
  'customers.find': 'Rechercher quelqu’un',
  'customers.findPlaceholder': 'Nom, téléphone ou e-mail',
  'customers.add': 'Ajouter un client',
  'customers.looking': 'Recherche…',
  'customers.denied':
    'Vous n’avez pas accès aux fiches clients. Demandez à un responsable si cela vous semble anormal.',
  'customers.failed': 'Impossible de charger les clients.',
  'customers.noMatches': 'Personne ne correspond.',
  'customers.colName': 'Nom',
  'customers.colKind': 'Type',
  'customers.colEmail': 'E-mail',
  'customers.colPhone': 'Téléphone',
  'customers.count': { one: '{count} client', other: '{count} clients' },
  'customers.countCapped': 'Les {count} premiers clients. Il peut y en avoir d’autres.',
  'customers.cappedNote':
    'Affichage des {count} premiers. Il peut y en avoir d’autres — affinez la recherche en attendant la pagination.',

  'customers.kindLabel': 'Particulier ou entreprise',
  'customers.firstName': 'Prénom',
  'customers.lastName': 'Nom',
  'customers.businessName': 'Raison sociale',
  'customers.email': 'E-mail',
  'customers.phone': 'Téléphone',
  'customers.submit': 'Ajouter',
  'customers.checking': 'Recherche de doublons…',
  'customers.adding': 'Ajout…',

  'customers.duplicateTitle': 'Quelqu’un de semblable existe déjà',
  'customers.duplicateLede':
    'Créer une seconde fiche pour la même personne divise son historique — son entretien, ses ventes et ses coordonnées cessent de concorder. Vérifiez si l’une de ces fiches est la bonne.',
  'customers.noContactDetails': 'aucune coordonnée',
  'customers.oneOfTheseIsThem': 'C’est l’une de ces fiches',
  'customers.addAnyway': 'Aucune de celles-ci — ajouter quand même',

  'enum.importOutcome.Pending': 'En attente',
  'enum.importOutcome.Created': 'Ajoutée',
  'enum.importOutcome.Updated': 'Déjà présente',
  'enum.importOutcome.Skipped': 'Ignorée',
  'enum.importOutcome.Failed': 'Refusée',

  'records.title': 'Données',
  'records.bringIn': 'Importer des données',
  'records.bringInLede':
    'Un tableur exporté de votre ancien système, enregistré en CSV. Rien n’est écrit tant que vous n’avez pas fait un essai à blanc.',
  'records.whatIsInIt': 'Contenu du fichier',
  'records.file': 'Le fichier',
  'records.unreadableFile': 'Ce fichier n’a pas pu être lu. S’agit-il bien d’un CSV texte ?',
  'records.practice': 'Essai à blanc',
  'records.practising': 'Essai en cours…',
  'records.importForReal': 'Importer pour de bon',
  'records.importing': 'Importation…',
  'records.practiseFirst':
    'Faites d’abord l’essai à blanc. Il ne change rien et vous dit exactement ce que fera l’import réel.',

  'records.takeOut': 'Exporter des données',
  'records.takeOutLede':
    'Télécharge tout ce qui est de ce type au format CSV. C’est exactement la structure que cette page réaccepte : vous pouvez donc emporter vos données où vous voulez, y compris vers un autre système.',
  'records.downloadCustomers': 'Télécharger les clients',
  'records.downloadVehicles': 'Télécharger les véhicules',

  'records.couldNotRun': 'Cet import n’a pas pu être exécuté.',
  'records.whatWouldHappen': 'Ce qui se passerait',
  'records.whatHappened': 'Ce qui s’est passé',
  'records.summaryPractice': {
    one: 'Sur {count} ligne : {created} seraient ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} illisibles.',
    other:
      'Sur {count} lignes : {created} seraient ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} illisibles.',
  },
  'records.summaryReal': {
    one: 'Sur {count} ligne : {created} ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} refusées.',
    other:
      'Sur {count} lignes : {created} ajoutées, {updated} déjà présentes, {skipped} ignorées, {failed} refusées.',
  },
  'records.nothingWritten': 'Rien n’a été écrit. C’était un essai à blanc.',
  'records.rowsToLookAt': 'Lignes à examiner',
  'records.rowsToLookAtLede':
    'Le numéro de ligne est celui que vous voyez dans votre tableur, et la ligne est citée telle qu’elle est arrivée. Corrigez le fichier et relancez — rien ici ne modifie ce que vous avez envoyé.',
  'records.problemCount': {
    one: '{count} ligne à examiner',
    other: '{count} lignes à examiner',
  },
  'records.colLine': 'Ligne',
  'records.colWhatHappened': 'Ce qui s’est passé',
  'records.colTheRow': 'La ligne',
  'records.timeout':
    'Cet import prend plus de temps que prévu. Il est toujours en cours — c’est simplement cette page qui a cessé d’attendre.',
};
