/**
 * Class names a driver meets, as a sim reports them, and the cuts a chip makes of them.
 *
 * Every run that draws a car's class draws it through `chipText`, which keeps the first four
 * letters of whichever name it is handed, so this list, and not the chip's `LMP2`, is what a
 * declared widest has to hold. It was `classAndPlace.test.ts`'s own until the blue flag's
 * position-and-class run was found measured from `LMP2` and drawing `LAMB` and `MCLA` past it
 * (#931); both tests now read it from here, so a class found on a rig is added once and every run
 * that draws a class is measured against it.
 */
import { CHIP_CHARS } from '../src/second/chip.ts';

/**
 * iRacing's multiclass classes, the single-make series that race as a class named after the car,
 * and the classes Assetto Corsa and ACC report. A class found on a rig that the list lacks belongs
 * here, and the tests that read it then say whether each declaration still holds it.
 */
export const CLASS_NAMES = [
  // Multiclass classes.
  'GTP', 'LMP2', 'LMP3', 'LMDh', 'Hypercar', 'GTE', 'GTD', 'GT3', 'GT3 Class', 'GT4', 'GT4 Class', 'TCR', 'IMSA',
  // Single-make classes, named after the car or its series.
  'Porsche 911 Cup', 'PCup', 'Mustang', 'Ford GT', 'Mercedes W13', 'Mercedes-AMG', 'McLaren MP4-30', 'BMW M4 GT4',
  'BMW M Hybrid V8', 'Lamborghini', 'Ferrari 296 GT3', 'Aston Martin', 'Audi RS 3 LMS', 'Cadillac V-Series.R',
  'Acura ARX-06', 'Toyota GR86', 'Mazda MX-5 Cup', 'Global Mazda MX-5 Cup', 'MX-5 Cup', 'Chevrolet Camaro',
  'Supercars', 'Formula Vee', 'Formula Renault', 'Super Formula', 'Dallara IR18', 'Dallara P217', 'Skip Barber',
  'Ray FF1600', 'Williams FW31', 'Lotus 79', 'Radical SR8', 'Ligier JS P320', 'Porsche 963', 'Kia Optima',
  'Renault Clio', 'Volkswagen Jetta', 'VW Beetle', 'Mini Cooper', 'NASCAR Cup', 'Xfinity', 'ARCA Menards',
  'Legends', 'Late Model', 'Street Stock', 'Modified', 'Sprint Car', 'Silver Crown', 'Midget', 'Pro Mazda',
  'Indy Pro 2000', 'USF 2000', 'HPD',
  // Assetto Corsa's and ACC's classes.
  'Race', 'Street', 'Drift', 'Vintage', 'Touring', 'Prototype', 'GT2', 'CUP', 'ST', 'CHL', 'TCX',
] as const;

/** Each class name as a chip draws it: its first four characters, in capitals, once each. */
export const CLASS_CUTS: readonly string[] = [...new Set(CLASS_NAMES.map((name) => name.slice(0, CHIP_CHARS).toUpperCase()))];
