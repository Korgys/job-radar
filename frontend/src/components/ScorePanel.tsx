import type { Score } from '../types';

export function ScorePanel({ score }: { score?: Score | null }) {
  if (!score) {
    return <p className="muted">Score non calculé.</p>;
  }

  return (
    <div className="score-panel">
      <div className="score-main">{score.globalScore}/100</div>
      <div className="score-grid">
        <span>Stack {score.stackScore}</span>
        <span>Rôle {score.roleScore}</span>
        <span>Domaine {score.domainScore}</span>
        <span>Séniorité {score.seniorityScore}</span>
        <span>Localisation {score.locationScore}</span>
        <span>Salaire {score.salaryScore}</span>
      </div>
      <ReasonList title="Points forts" values={score.positiveReasons} />
      <ReasonList title="Points faibles" values={score.negativeReasons} />
      <ReasonList title="Compétences manquantes" values={score.missingSkills} />
    </div>
  );
}

function ReasonList({ title, values }: { title: string; values: string[] }) {
  if (values.length === 0) {
    return null;
  }

  return (
    <div className="reason-list">
      <strong>{title}</strong>
      <ul>
        {values.slice(0, 6).map((value) => (
          <li key={value}>{value}</li>
        ))}
      </ul>
    </div>
  );
}
