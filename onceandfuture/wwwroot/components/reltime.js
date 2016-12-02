var React = require('react'); // N.B. Still need this because JSX.
import moment from 'moment'

const RelTime = ({time}) => {
    const timeAgo = moment(time).fromNow();
    return <span>{timeAgo}</span>;
}

export default RelTime;