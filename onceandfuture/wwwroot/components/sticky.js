import React from 'react';

function findChild(childs, offset) {
  let min = 0;
  let max = childs.length - 1;
  while(min <= max)
  {
    const index = min + Math.floor((max - min) / 2);
    const child = childs[index];
    let indexOffset = child.getOffsetTop();
    if (indexOffset === offset) {
      return child;
    } else if (indexOffset > offset) {
      max = index - 1;
    } else {
      min = index + 1;
    }
  }

  if (max < 0) { return null; }
  return childs[max];
}

export class StickyContainer extends React.Component {
  constructor(props) {
    super(props);
    this.state = { scrollTop: 0, titles: [], sorted: true };
    this.handleScroll = this.handleScroll.bind(this);
  }

  getChildContext() {
    return { stickyContainer: this };
  }

  childDidMount(child) {
    this.setState(prevState => {
      const newTitles = [].concat(prevState.titles, child);
      return this.handleUpdate(newTitles);
    });
  }

  childDidUpdate() {
    this.setState(prevState => this.handleUpdate(prevState.titles));
  }

  handleUpdate(newTitles) {
    newTitles.sort((a,b) => a.getOffsetTop() - b.getOffsetTop());
    return { titles: newTitles };
  }

  childWillUnmount(child) {
    return this.setState(prevState => {
      const index = this.state.titles.findIndex(c => c === child);
      if (index >= 0) {
        const newTitles = Array.from(prevState.titles);
        newTitles.splice(index, 1);
        return { titles: newTitles };
      } else {
        return {};
      }
    });
  }

  handleScroll(event) {
    this.lastScrollPosition = event.target.scrollTop;
    if (!this.ticking) {
      window.requestAnimationFrame(() => {
        this.setState({ scrollTop: this.lastScrollPosition });
        this.ticking = false;
      });
    }
    this.ticking = true;
  }

  render() {
    let stuck = findChild(this.state.titles, this.state.scrollTop);
    // if (!stuck) { stuck = this.state.titles[0]; }
    let stuckChild = null;
    let stuckWidth = 'auto';
    if (stuck) {
      stuckWidth = stuck.getWidth();
      stuckChild = <div style={stuck.props.style}>{stuck.props.children}</div>;
    }

    const pinstyle = Object.assign({}, this.props.style, {
      bottom: undefined,
      height: 'auto',
      right: undefined,
      width: stuckWidth,
    });

    return <div>
      <div style={this.props.style} onScroll={this.handleScroll}>
        {this.props.children}
      </div>
      <div style={pinstyle}>{stuckChild}</div>
    </div>;
  }
}

StickyContainer.childContextTypes = {
  stickyContainer: React.PropTypes.object
};

export class StickyTitle extends React.Component {
  constructor(props) {
    super(props);
    this.gotElement = (element) => { this.placeholder = element; };
  }

  componentDidMount() {
    this.context.stickyContainer.childDidMount(this);
  }

  componentWillUnmount() {
    this.context.stickyContainer.childWillUnmount(this);
  }

  componentDidUpdate() {
    this.context.stickyContainer.childDidUpdate(this);
  }

  getOffsetTop() { return this.placeholder.offsetTop; }

  // The 3 here is a margin or padding or something I forget anyway it should
  // be read but I don't know how.
  getWidth() { return this.placeholder.offsetWidth + 3; }

  render() {
    return <div ref={this.gotElement} style={this.props.style}>{this.props.children}</div>;
  }
}

StickyTitle.contextTypes = {
  stickyContainer: React.PropTypes.object
};
