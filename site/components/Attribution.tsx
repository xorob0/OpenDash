/**
 * The car light data's attribution, wherever the lights are described. The data is CC BY-NC-SA
 * 4.0, and the licence asks for the credit and the link, so both are here and nowhere else.
 */
import { CAR_DATA_CREDIT, CAR_DATA_URL } from '../lib/site';

export function Attribution({ className }: { className?: string }) {
  return (
    <p className={className}>
      {CAR_DATA_CREDIT}{' '}
      <a href={CAR_DATA_URL} className="link" rel="noopener">
        Lovely Car Data on GitHub
      </a>
      .
    </p>
  );
}
