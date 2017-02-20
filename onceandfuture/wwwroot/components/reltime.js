import React from 'react';

const MsPerSecond = 1000;
const SecondsPerMinute = 60;
const MinutesPerHour = 60;
const HoursPerDay = 60;
const DaysPerWeek = 7;

const MsPerMinute = MsPerSecond * SecondsPerMinute;
const MsPerHour = MsPerMinute * MinutesPerHour;
const MsPerDay = MsPerHour * HoursPerDay;
const MsPerWeek = MsPerDay * DaysPerWeek;

class RelTime extends React.Component {
  constructor(props) {
    super(props);
    this.state = { now: Date.now() };
  }

  componentDidMount() {
    const interval = this.nextIntervalMs(Date.now());
    this.timerID = setTimeout(() => this.tick(), interval);
  }

  componentWillUnmount() {
    clearTimeout(this.timerID);
  }

  tick() {
    const now = Date.now();
    const interval = this.nextIntervalMs(now);

    this.setState({ now: now });
    this.timerID = setTimeout(() => this.tick(), interval);
  }

  nextIntervalMs(now) {
    const parsed = Date.parse(this.props.time);
    const delta = now - parsed;
    if (delta > MsPerWeek) { return MsPerWeek; }
    if (delta > MsPerDay) { return MsPerDay; }
    if (delta > MsPerHour) { return MsPerHour; }

    // Watching the seconds tick by is fun and all, but let's not go
    // crazy. Minute-by-minute updates are fine, and don't ruin anything
    // really.
    return MsPerMinute;
  }

  render() {
    const time = this.props.time;
    const parsed = Date.parse(time);
    const delta = this.state.now - parsed;

    const sentence = this.sentenceForDelta(delta);

    return <span>{sentence}</span>;
  }

  sentenceForDelta(delta) {
    var unit, value;
    if (delta > MsPerWeek) {
      value = Math.round(delta / MsPerWeek);
      unit = 'week';
    } else if (delta > MsPerDay) {
      value = Math.round(delta / MsPerDay);
      if (value === 1) { return "yesterday"; }
      unit = 'day';
    } else if (delta > MsPerHour) {
      value = Math.round(delta / MsPerHour);
      unit = 'hour';
    } else if (delta > MsPerMinute) {
      value = Math.round(delta / MsPerMinute);
      unit = 'minute';
    } else if (delta > MsPerSecond) {
      value = Math.round(delta / MsPerSecond);
      unit = 'second';
    } else {
      return "just now";
    }

    if (value > 1) { unit = unit + "s"; }
    return value + " " + unit + " ago";
  }
}

export default RelTime;
