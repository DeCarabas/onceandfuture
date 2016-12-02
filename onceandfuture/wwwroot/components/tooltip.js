var React = require('react');
import {
  COLUMNSPACER,
  COLUMNWIDTH,
  COLOR_DARK,
  COLOR_VERY_LIGHT,
  COLOR_VERY_DARK,
} from './style'

const TIP_STYLE = {
  backgroundColor: COLOR_VERY_DARK,
  color: COLOR_VERY_LIGHT,
  textAlign: 'center',
  padding: '5px 10px',
  borderRadius: '6px',
  display: 'inline-block',
  position: 'absolute',
  zIndex: 1,
  top: -5,
  left: '105%', 
};

const DIV_STYLE = {
  position: 'relative',
};

class Tooltip extends React.Component {
  constructor(props) {
    super(props);
    this.state = { inside: false };

    this.handleMouseEnter = this.handleMouseEnter.bind(this);
    this.handleMouseLeave = this.handleMouseLeave.bind(this);
  }

  render() {
    let tip = <span />;
    if (this.state.inside) {
      tip = <span style={TIP_STYLE}>
        {this.props.tip}
      </span>; 
    }

    return <div style={DIV_STYLE} onMouseEnter={this.handleMouseEnter} onMouseLeave={this.handleMouseLeave}>
      {tip}
      {this.props.children}
    </div>
  }

  handleMouseEnter() {
    this.setState(prevState => ({ inside: true }));
  }

  handleMouseLeave() {
    this.setState(prevState => ({ inside: false }));
  }
}

export default Tooltip;