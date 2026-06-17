import type { Score } from '../types';

export function ScorePanel({ score }: { score?: Score | null }) {
  if (!score) {
    return <p className="muted">Score non calculé.</p>;
  }

  return (
    <div className="score-panel">
      <div className="score-main">{score.globalScore}/100</div>
      <div className="score-grid">
        <span>Technique {score.stackScore}/40</span>
        <span>Expérience {score.seniorityScore}/30</span>
        <span>Rôle {score.roleScore}/20</span>
        <span>Domaine {score.domainScore}/10</span>
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
