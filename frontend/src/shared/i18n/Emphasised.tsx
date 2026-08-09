// Emphasised — a translated sentence with one part of it picked out in bold.
//
// Use:  <Emphasised sentence={t('parts.costingApplies', { … })} value={theBit} />
//
// Edit: this exists because the obvious alternative is wrong in four of our five
//       languages. Writing
//
//         <>From the enquiry for <strong>{name}</strong>. It will be linked.</>
//
//       hard-codes English word order into the JSX. A German translator needs
//       the verb at the end and an Arabic one needs the whole clause reversed,
//       and neither can move a fragment that lives in a component tree.
//
//       So the FINISHED sentence is translated as one string, and the emphasis
//       is found inside it afterwards. The translator puts the value wherever
//       their language wants it, and the bold follows it there.
//
//       `indexOf` rather than `split`, so a value that happens to occur twice
//       produces exactly one emphasised run rather than two. A value that is not
//       found at all renders the sentence plainly — a missing bold is a
//       cosmetic loss, and throwing would take a working screen down over one.

export function Emphasised({ sentence, value }: { sentence: string; value: string }) {
  const at = value === '' ? -1 : sentence.indexOf(value);
  if (at < 0) {
    return <>{sentence}</>;
  }

  return (
    <>
      {sentence.slice(0, at)}
      <span className="strong">{value}</span>
      {sentence.slice(at + value.length)}
    </>
  );
}
