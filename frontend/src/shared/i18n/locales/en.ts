// en — the English catalogue, and the schema every other language is checked
// against.
//
// Use:  t('nav.stock'). Keys are `area.thing`, lower case, dot separated.
// Edit: adding a key here makes `npm run typecheck` fail for fr, de, ru and ar
//       until each has one too. That is the point — a missing translation is a
//       build failure rather than an English sentence surfacing mid-screen.
//
//       Write the ENGLISH first and write it well. Every other catalogue is
//       translated from this text, so a vague or clipped phrase here becomes
//       four vague or clipped phrases. Prefer a whole sentence over a fragment
//       assembled at the call site: "{n} of {total}" cannot be reordered by a
//       translator, and German and Arabic both need to reorder it.
//
//       A plural entry is an object, never an `if (n === 1)` at the call site.
//       Russian needs four forms and Arabic six.

export const en = {
  // --- Words the whole application uses -------------------------------------
  'app.name': 'DealerFOSS',
  'common.loading': 'Loading…',
  'common.save': 'Save',
  'common.saving': 'Saving…',
  'common.cancel': 'Cancel',
  'common.close': 'Close',
  'common.retry': 'Try again',
  'common.search': 'Search',
  'common.searching': 'Searching…',
  'common.none': 'None',
  'common.all': 'All',
  'common.yes': 'Yes',
  'common.no': 'No',
  'common.back': 'Back',
  'common.continue': 'Continue',
  'common.unexpected': 'Something went wrong. Try again.',
  'common.notPermitted': 'You do not have permission to see this.',
  'common.unreachable': 'Could not reach the server. Is it running?',

  // --- Refusals the reader sees on any screen -------------------------------
  // See apiMessage.ts for why these are translated and the rest are not.
  'error.network': 'Could not reach the server. Is it running?',
  'error.invalidCredentials': 'That email address and password do not match an account.',
  'error.sessionRequired': 'Sign in to use this.',
  'error.adminSessionRequired':
    'Sign in as an administrator to use the control plane. A dealership sign-in does not reach it.',
  'error.sessionInvalid': 'Your session has ended. Sign in again.',
  'error.antiForgeryFailed':
    'This browser no longer holds a token matching its session. Sign in again.',
  'error.secondFactorRejected': 'That code was not accepted.',
  'error.secondFactorRequired':
    'Your role requires two-step sign-in. Set it up to reach the rest of the application.',
  'error.mfaNotEnrolled': 'Two-step sign-in is not set up on this account.',
  'error.mfaAlreadyOn': 'Two-step sign-in is already on for this account.',
  'error.notATenantCaller': 'An administrator cannot act as a dealership user.',
  'error.tenantRequired': 'Say which dealer group this is.',
  'error.tenantNotFound': 'No active dealer group matches that name.',

  // --- The shell ------------------------------------------------------------
  'shell.skipToContent': 'Skip to content',
  'shell.mainNavigation': 'Main',
  'shell.signOut': 'Sign out',
  'shell.shortcuts': 'Shortcuts',
  'shell.shortcutsTitle': 'Keyboard shortcuts ( ? )',
  'shell.language': 'Language',
  'shell.appearance': 'Appearance',

  'nav.dashboard': 'This month',
  'nav.customers': 'Customers',
  'nav.leads': 'Enquiries',
  'nav.deals': 'Deals',
  'nav.stock': 'Stock',
  'nav.workshop': 'Workshop',
  'nav.parts': 'Parts',
  'nav.trialBalance': 'Trial balance',
  'nav.books': 'The books',
  'nav.records': 'Records',
  'nav.staff': 'People',
  'nav.secondFactor': 'Two-step sign-in',

  // --- Appearance -----------------------------------------------------------
  'appearance.auto': 'Auto',
  'appearance.autoHint': 'Follow this machine',
  'appearance.light': 'Light',
  'appearance.lightHint': 'Always light',
  'appearance.dark': 'Dark',
  'appearance.darkHint': 'Always dark',

  // --- Signing in -----------------------------------------------------------
  'signIn.lede': 'Sign in to your dealership.',
  'signIn.dealerGroup': 'Dealer group',
  'signIn.email': 'Email',
  'signIn.password': 'Password',
  'signIn.submit': 'Sign in',
  'signIn.submitting': 'Signing in…',
  'signIn.codeLede':
    'Enter the six-digit code from your authenticator app, or one of your recovery codes.',
  'signIn.code': 'Code',
  'signIn.checking': 'Checking…',
  'signIn.startAgain': 'Start again',

  // --- Setting a first password ---------------------------------------------
  'setPassword.title': 'Set your password',
  'setPassword.lede':
    'Your manager gave you a code. Use it once here to choose a password only you know — nobody at the dealership can see what you pick.',
  'setPassword.dealership': 'Dealership',
  'setPassword.email': 'Email',
  'setPassword.code': 'Code',
  'setPassword.password': 'New password',
  'setPassword.passwordHint': 'At least 12 characters. Length is what makes one hard to guess.',
  'setPassword.again': 'New password again',
  'setPassword.mismatch': 'Those two do not match.',
  'setPassword.failed': 'That did not work.',
  'setPassword.submit': 'Set my password',
  'setPassword.doneTitle': 'You are set up',
  'setPassword.doneLede': 'Sign in with your email address and the password you just chose.',
  'setPassword.toSignIn': 'Go to sign in',

  // --- Two-step sign-in -----------------------------------------------------
  'secondFactor.title': 'Two-step sign-in',
  'secondFactor.required':
    'Your dealership requires two-step sign-in for your role. Until you set it up, this is the only screen you can use.',
  'secondFactor.intro':
    'After this, signing in asks for a six-digit code from an app on your phone as well as your password. Google Authenticator, Authy and 1Password all work.',
  'secondFactor.start': 'Start',
  'secondFactor.starting': 'Starting…',
  'secondFactor.pointApp': 'Point your authenticator app at this square.',
  'secondFactor.qrTitle': 'Scan this with your authenticator app',
  'secondFactor.cannotScan': 'Can’t scan it?',
  'secondFactor.typeInstead': 'Type this into the app by hand instead:',
  'secondFactor.enterCode': 'Now enter the code it shows',
  'secondFactor.turnOn': 'Turn it on',
  'secondFactor.checking': 'Checking…',
  'secondFactor.notYet':
    'Nothing has changed about signing in yet. It only takes effect once the code above is accepted.',
  'secondFactor.onNow':
    'Two-step sign-in is on. From now on you will be asked for a code after your password.',
  'secondFactor.saveTitle': 'Save these somewhere safe',
  'secondFactor.saveLede':
    'Each of these works once, and only if you lose your phone. This is the only time they will ever be shown.',
  'secondFactor.recoveryCodes': 'Recovery codes',

  // --- Keyboard shortcuts ---------------------------------------------------
  'shortcuts.title': 'Keyboard shortcuts',
  'shortcuts.space': 'Space',
  'shortcuts.note':
    'Shortcuts are ignored while you are typing in a field, so they never eat a character you meant to write.',
  'shortcuts.goDashboard': 'Go to the dashboard',
  'shortcuts.goStock': 'Go to stock',
  'shortcuts.goCustomers': 'Go to customers',
  'shortcuts.goLeads': 'Go to enquiries',
  'shortcuts.goDeals': 'Go to deals',
  'shortcuts.goWorkshop': 'Go to the workshop',
  'shortcuts.goParts': 'Go to parts',
  'shortcuts.goBooks': 'Go to the books',
  'shortcuts.monthBefore': 'On the dashboard: the month before',
  'shortcuts.monthAfter': 'On the dashboard: the month after',
  'shortcuts.thisMonth': 'On the dashboard: back to this month',
  'shortcuts.showList': 'Show this list',

  // --- Words the API sends back ---------------------------------------------
  // The server answers with `"OnHold"`, and a screen that printed that verbatim
  // would say "OnHold" in every language including English. These are the only
  // place an API enum is turned into something a person reads; see
  // shared/i18n/enums.ts for the lookup that guarantees no value is missed.
  'enum.inventoryStatus.Incoming': 'Incoming',
  'enum.inventoryStatus.Reconditioning': 'Reconditioning',
  'enum.inventoryStatus.Available': 'Available',
  'enum.inventoryStatus.OnHold': 'On hold',
  'enum.inventoryStatus.Sold': 'Sold',
  'enum.inventoryStatus.Removed': 'Removed',

  'enum.leadStatus.New': 'New',
  'enum.leadStatus.Working': 'Working',
  'enum.leadStatus.Appointment': 'Appointment',
  'enum.leadStatus.Won': 'Won',
  'enum.leadStatus.Lost': 'Lost',

  'enum.leadSource.WalkIn': 'Walk-in',
  'enum.leadSource.Phone': 'Phone',
  'enum.leadSource.Website': 'Website',
  'enum.leadSource.Referral': 'Referral',
  'enum.leadSource.Marketplace': 'Marketplace',
  'enum.leadSource.Unknown': 'Unknown',

  'enum.dealStatus.Draft': 'Draft',
  'enum.dealStatus.Submitted': 'Submitted',
  'enum.dealStatus.Approved': 'Approved',
  'enum.dealStatus.Delivered': 'Delivered',
  'enum.dealStatus.Lost': 'Lost',

  'enum.chargeKind.VehiclePrice': 'Vehicle price',
  'enum.chargeKind.Fee': 'Fee',
  'enum.chargeKind.Discount': 'Discount',
  'enum.chargeKind.Accessory': 'Accessory',

  'enum.repairOrderStatus.Booked': 'Booked',
  'enum.repairOrderStatus.InProgress': 'In progress',
  'enum.repairOrderStatus.Completed': 'Completed',
  'enum.repairOrderStatus.Invoiced': 'Invoiced',
  'enum.repairOrderStatus.Cancelled': 'Cancelled',

  'enum.serviceLineKind.Labour': 'Labour',
  'enum.serviceLineKind.Part': 'Part',
  'enum.serviceLineKind.Sublet': 'Sent out',

  'enum.financeProductKind.Warranty': 'Warranty',
  'enum.financeProductKind.Gap': 'GAP',
  'enum.financeProductKind.ServicePlan': 'Service plan',
  'enum.financeProductKind.Protection': 'Protection',
  'enum.financeProductKind.Other': 'Other',

  'enum.periodState.Open': 'Open',
  'enum.periodState.Closed': 'Closed',

  'enum.booksState.NotOpened': 'Not opened',
  'enum.booksState.Open': 'Open',
  'enum.booksState.Closed': 'Closed',
  'enum.booksState.Unknown': 'Unknown',

  'enum.importKind.Customers': 'Customers',
  'enum.importKind.Vehicles': 'Vehicles',

  'enum.tenantStatus.Active': 'Active',
  'enum.tenantStatus.Suspended': 'Suspended',
  'enum.tenantStatus.Provisioning': 'Provisioning',
  'enum.tenantStatus.Archived': 'Archived',

  // --- Stock ----------------------------------------------------------------
  'stock.title': 'Stock',
  'stock.status': 'Status',
  'stock.loading': 'Loading the stock list…',
  'stock.denied':
    'You do not have access to this location’s stock. Ask a manager if you think that is wrong.',
  'stock.failed': 'Could not load the stock list.',
  'stock.empty': 'Nothing here yet. Cars appear once they are taken into stock.',
  'stock.onlyStockNumber': 'Showing stock number {stock} only.',
  'stock.showEverything': 'Show everything',
  'stock.colStock': 'Stock',
  'stock.colVehicle': 'Vehicle',
  'stock.colVin': 'VIN',
  'stock.colStatus': 'Status',
  'stock.count': { one: '{count} car in stock', other: '{count} cars in stock' },
  'stock.countCapped': 'The first {count} cars in stock. There may be more.',
  'stock.cappedNote':
    'Showing the first {count}. There may be more — narrow it with the status filter until paging exists.',

  'enum.accountKind.Asset': 'Asset',
  'enum.accountKind.Liability': 'Liability',
  'enum.accountKind.Equity': 'Equity',
  'enum.accountKind.Revenue': 'Revenue',
  'enum.accountKind.Expense': 'Expense',

  // --- Trial balance --------------------------------------------------------
  'trialBalance.title': 'Trial balance',
  'trialBalance.loading': 'Adding it up…',
  'trialBalance.denied': 'You do not have access to these figures.',
  'trialBalance.failed': 'Could not load the balances.',
  'trialBalance.empty': 'Nothing posted yet. Entries appear here once a car has been delivered.',
  'trialBalance.inBalance': 'In balance — debits and credits both come to {total}.',
  'trialBalance.outOfBalance':
    'Out of balance by {difference}. Something was lost on the way in.',
  'trialBalance.colCode': 'Code',
  'trialBalance.colAccount': 'Account',
  'trialBalance.colKind': 'Kind',
  'trialBalance.colDebits': 'Debits',
  'trialBalance.colCredits': 'Credits',
  'trialBalance.colBalance': 'Balance',
  'trialBalance.total': 'Total',

  // --- Customers ------------------------------------------------------------
  'enum.customerKind.Person': 'Person',
  'enum.customerKind.Business': 'Business',

  'customers.title': 'Customers',
  'customers.find': 'Find someone',
  'customers.findPlaceholder': 'Name, phone, or email',
  'customers.add': 'Add a customer',
  'customers.looking': 'Looking…',
  'customers.denied':
    'You do not have access to customer records. Ask a manager if you think that is wrong.',
  'customers.failed': 'Could not load customers.',
  'customers.noMatches': 'Nobody matches that.',
  'customers.colName': 'Name',
  'customers.colKind': 'Kind',
  'customers.colEmail': 'Email',
  'customers.colPhone': 'Phone',
  'customers.count': { one: '{count} customer', other: '{count} customers' },
  'customers.countCapped': 'The first {count} customers. There may be more.',
  'customers.cappedNote':
    'Showing the first {count}. There may be more — narrow the search until paging exists.',

  'customers.kindLabel': 'Person or business',
  'customers.firstName': 'First name',
  'customers.lastName': 'Last name',
  'customers.businessName': 'Business name',
  'customers.email': 'Email',
  'customers.phone': 'Phone',
  'customers.submit': 'Add',
  'customers.checking': 'Checking for duplicates…',
  'customers.adding': 'Adding…',

  'customers.duplicateTitle': 'Somebody like this is already here',
  'customers.duplicateLede':
    'Adding a second record for the same person splits their history — their service, their deals, and their contact details stop agreeing. Check whether one of these is them.',
  'customers.noContactDetails': 'no contact details',
  'customers.oneOfTheseIsThem': 'One of these is them',
  'customers.addAnyway': 'None of these — add anyway',

  // --- Bringing records in and taking them out ------------------------------
  'enum.importOutcome.Pending': 'Pending',
  'enum.importOutcome.Created': 'Added',
  'enum.importOutcome.Updated': 'Already here',
  'enum.importOutcome.Skipped': 'Skipped',
  'enum.importOutcome.Failed': 'Refused',

  'records.title': 'Records',
  'records.bringIn': 'Bring records in',
  'records.bringInLede':
    'A spreadsheet exported from your old system, saved as CSV. Nothing is written until you have run it as a practice first.',
  'records.whatIsInIt': 'What is in the file',
  'records.file': 'The file',
  'records.unreadableFile': 'That file could not be read. Is it a text CSV?',
  'records.practice': 'Practice run',
  'records.practising': 'Trying it…',
  'records.importForReal': 'Import for real',
  'records.importing': 'Importing…',
  'records.practiseFirst':
    'Run the practice first. It changes nothing and tells you exactly what the real one will do.',

  'records.takeOut': 'Take records out',
  'records.takeOutLede':
    'Downloads everything of that kind as a CSV. It is the same shape this page accepts back, so you can move it anywhere — including into another system entirely.',
  'records.downloadCustomers': 'Download customers',
  'records.downloadVehicles': 'Download vehicles',

  'records.couldNotRun': 'That import could not be run.',
  'records.whatWouldHappen': 'What would happen',
  'records.whatHappened': 'What happened',
  // One sentence rather than five fragments joined at the call site: German and
  // Arabic both need to reorder it, and a comma-separated list assembled in
  // English word order cannot be reordered by a translator.
  'records.summaryPractice': {
    one: 'Of {count} row: {created} would be added, {updated} already here, {skipped} skipped, {failed} could not be read.',
    other:
      'Of {count} rows: {created} would be added, {updated} already here, {skipped} skipped, {failed} could not be read.',
  },
  'records.summaryReal': {
    one: 'Of {count} row: {created} added, {updated} already here, {skipped} skipped, {failed} refused.',
    other:
      'Of {count} rows: {created} added, {updated} already here, {skipped} skipped, {failed} refused.',
  },
  'records.nothingWritten': 'Nothing has been written. This was a practice run.',
  'records.rowsToLookAt': 'Rows to look at',
  'records.rowsToLookAtLede':
    'The line number is the one you see in your spreadsheet, and the row is quoted exactly as it arrived. Fix the file and run it again — nothing here edits what you sent.',
  'records.problemCount': {
    one: '{count} row needing attention',
    other: '{count} rows needing attention',
  },
  'records.colLine': 'Line',
  'records.colWhatHappened': 'What happened',
  'records.colTheRow': 'The row',
  'records.timeout':
    'That import is taking longer than expected. It is still running — this page just stopped waiting.',
} as const;
