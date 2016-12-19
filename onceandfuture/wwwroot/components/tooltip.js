import React from 'react';
import {
  COLOR_VERY_LIGHT,
  COLOR_VERY_DARK,
  TEXT_FONT_SIZE,

  Z_INDEX_TOOLTIP,
} from './style';

const DIV_STYLE = {
  display: 'inline-block',
  position: 'relative',
};

const TIP_STYLE_BASE = {
  backgroundColor: COLOR_VERY_DARK,
  borderRadius: '6px',
  color: COLOR_VERY_LIGHT,
  display: 'inline-block',
  padding: '5px 10px',
  position: 'absolute',
  textAlign: 'center',
  zIndex: Z_INDEX_TOOLTIP,
  fontSize: TEXT_FONT_SIZE,
  fontWeight: 'normal',
};

class Tooltip extends React.Component {
  constructor(props) {
    super(props);
    this.state = { inside: false };
    this.position = props.position || 'left';
    this.width = props.width || 120;

    this.handleMouseEnter = this.handleMouseEnter.bind(this);
    this.handleMouseLeave = this.handleMouseLeave.bind(this);
  }

  render() {
    var TIP_STYLE;
    if (this.position === 'right') {
      TIP_STYLE = Object.assign({}, TIP_STYLE_BASE, {
        top: -5,
        left: '105%',
        width: this.width,
      });
    } else if (this.position === 'top') {
      TIP_STYLE = Object.assign({}, TIP_STYLE_BASE, {
        bottom: '100%',
        left: '50%',
        width: this.width,
        marginLeft: -60,
      });
    } else if (this.position === 'bottom') {
      TIP_STYLE = Object.assign({}, TIP_STYLE_BASE, {
        top: '100%',
        left: '50%',
        width: this.width,
        marginLeft: -60,
      });
    } else if (this.position === 'bottomleft') {
      TIP_STYLE = Object.assign({}, TIP_STYLE_BASE, {
        top: '100%',
        right: '105%',
        width: this.width,
        marginLeft: -60,
      });
    } else {
      // Default to left.
      TIP_STYLE = Object.assign({}, TIP_STYLE_BASE, {
        top: -5,
        right: '105%',
        width: this.width,
      });
    }

    let tip = <span />;
    if (this.state.inside && this.props.tip) {
      tip = <span style={TIP_STYLE}>
        {this.props.tip}
      </span>;
    }

    return <div style={DIV_STYLE}>
      {tip}
      <div onMouseEnter={this.handleMouseEnter} onMouseLeave={this.handleMouseLeave}>
        {this.props.children}
      </div>
    </div>;
  }

  handleMouseEnter() {
    this.setState(() => ({ inside: true }));
  }

  handleMouseLeave() {
    this.setState(() => ({ inside: false }));
  }
}

export default Tooltip;
