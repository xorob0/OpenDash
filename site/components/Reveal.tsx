'use client';

/**
 * Reveal: add `.in` to a subtree the first time it comes into view.
 *
 * The animation itself is two lines of CSS in globals.css and this only trips it, which is why the
 * resting state is the visible one: with JavaScript off, or with reduced motion asked for, the
 * class is never added and never needed, and the content is simply there. A reveal that hides
 * content until a script runs is a reveal that sometimes hides it for good.
 *
 * It fires once and disconnects. Content that re-animates every time it is scrolled past is the
 * thing that makes a long page tiring to read.
 */
import { useEffect, useRef, useState, type ElementType, type ReactNode } from 'react';

export function Reveal({
  children,
  as: Tag = 'div',
  delay = 0,
  className = '',
}: {
  children: ReactNode;
  as?: ElementType;
  /** Milliseconds, for staggering a row of siblings. Kept small: a long stagger reads as lag. */
  delay?: number;
  className?: string;
}) {
  const ref = useRef<HTMLElement>(null);
  const [shown, setShown] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node) return;
    // An element already in view on load should not wait for a scroll that may never come.
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) {
          setShown(true);
          observer.disconnect();
        }
      },
      { rootMargin: '0px 0px -12% 0px' },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, []);

  return (
    <Tag
      ref={ref}
      className={`reveal ${shown ? 'in' : ''} ${className}`.trim()}
      style={delay ? { transitionDelay: `${delay}ms` } : undefined}
    >
      {children}
    </Tag>
  );
}
